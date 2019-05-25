using Octovisor.Client.Exceptions;
using Octovisor.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octovisor.Client
{
    /// <summary>
    /// The base Octovisor client containing the socket logic
    /// </summary>
    public abstract class BaseClient
    {
        private volatile TcpClient Client;
        private volatile Task ReceivingTask;
        private volatile NetworkStream Stream;
        private volatile bool IsConnected;
        private volatile TaskCompletionSource<bool> RegisterTCS;
        private volatile TaskCompletionSource<bool> UnregisterTCS;
        private volatile TaskCompletionSource<List<RemoteProcessData>> RequestProcessesInfoTCS;

        private readonly byte[] Buffer;
        private readonly OctoConfig Config;
        private readonly MessageReader Reader;

        /// <summary>
        /// Fired when something is logged
        /// </summary>
        public event Action<LogMessage> Log;

        /// <summary>
        /// Fired when the client is connected to the remote server
        /// </summary>
        public event Func<Task> Connected;

        /// <summary>
        /// Fired when the client is registered on the remote server
        /// </summary>
        public event Func<Task> Registered;

        /// <summary>
        /// Fired when the client gets disconnected from the remote server
        /// </summary>
        public event Func<Task> Disconnected;

        internal event Action<ProcessUpdateData, bool> ProcessUpdate;
        internal event Action<List<RemoteProcessData>> ProcessesInfoReceived;
        internal event Func<Message, string> MessageRequestReceived;
        internal event Action<Message> MessageResponseReceived;
       
        internal bool IsConnectedInternal { get => this.IsConnected; }

        internal bool IsRegisteredInternal { get; private set; }

        internal MessageFactory MessageFactory { get; private set; }

        /// <summary>
        /// Creates a new instance of OctovisorClient
        /// </summary>
        /// <param name="config">A config object containing your token and other settings</param>
        protected BaseClient(OctoConfig config)
        {
            if (!config.IsValid())
                throw new Exception("Invalid Octovisor client configuration");

            this.Config = config;
            this.Reader = new MessageReader(config.MessageFinalizer);
            this.Buffer = new byte[config.BufferSize];
            this.MessageFactory = new MessageFactory(config.CompressionThreshold);
            this.IsRegisteredInternal = false;
            this.IsConnected = false;
        }

        protected void LogEvent(LogSeverity severity, string log) 
            => this.Log?.Invoke(new LogMessage(severity, log));

        /// <summary>
        /// Connects asynchronously to the Octovisor server
        /// </summary>
        public async Task ConnectAsync()
        {
            if (this.IsConnected)
                throw new AlreadyConnectedException();

            this.Client = new TcpClient();
            await this.Client.ConnectAsync(this.Config.Address, this.Config.Port);
            this.IsConnected = true;

            this.Stream = this.Client.GetStream();
            if (this.Connected != null)
                await this.Connected.Invoke();

            this.ReceivingTask = this.ListenAsync();

            await this.RegisterAsync();
            if (this.IsRegisteredInternal)
            {
                this.LogEvent(LogSeverity.Info, "Ready");

                if (this.Registered != null)
                    await this.Registered.Invoke();
            }
            else
            {
                this.LogEvent(LogSeverity.Info, "Server refused our credentials, disconnecting");
                await this.DisposeAsync();
            }
        }

        /// <summary>
        /// Disconnects asynchronously from the Octovisor server
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (!this.IsConnected || !this.IsRegisteredInternal)
                throw new NotConnectedException();

            if (this.GetTcpState() == TcpState.Established) // lets be nice if we can be
                await this.UnregisterAsync();
            else
                this.IsRegisteredInternal = false;

            await this.DisposeAsync();
            this.LogEvent(LogSeverity.Info, "Disconnected");

            if (this.Disconnected != null)
                await this.Disconnected.Invoke();
        }

        private async Task DisposeAsync()
        {
            this.IsConnected = false;
            await this.ReceivingTask;

            this.Stream.Dispose();
            this.Client.Dispose();
            this.Reader.Clear();
            this.ReceivingTask = null;
        }

        private void ClearBuffer()
            => Array.Clear(this.Buffer, 0, this.Config.BufferSize);

        private async Task CompleteProcessUpdateTCSAsync(ProcessUpdateData updateData, TaskCompletionSource<bool> tcs)
        {
            if (updateData.Accepted && updateData.Name.Equals(this.Config.ProcessName))
            {
                if (tcs != null)
                    tcs?.SetResult(updateData.Accepted);
                else
                    await this.DisconnectAsync();
            }
        }

        private async Task HandleUpdateProcessMessageAsync(Message msg, bool isRegisterUpdate)
        {
            if (msg.HasException) return; //timeout

            ProcessUpdateData updateData = msg.GetData<ProcessUpdateData>();
            await this.CompleteProcessUpdateTCSAsync(updateData, isRegisterUpdate ? this.RegisterTCS : this.UnregisterTCS);

            if (updateData.Accepted && !updateData.Name.Equals(this.Config.ProcessName))
                this.ProcessUpdate?.Invoke(updateData, isRegisterUpdate);
        }

        private void HandleRequestProcessesMessage(Message msg)
        {
            if (msg.HasException) return; //timeout

            List<RemoteProcessData> processesData = msg.GetData<List<RemoteProcessData>>();
            this.RequestProcessesInfoTCS?.SetResult(processesData);
        }

        private async Task SendResponseMessageAsync(Message msg)
        {
            string data;
            MessageStatus status;
            try
            {
                data = this.MessageRequestReceived?.Invoke(msg);
                status = MessageStatus.Unknown;
            }
            catch (Exception ex)
            {
                data = ex.Message;
                status = MessageStatus.TargetError;
            }

            Message responseMsg = this.MessageFactory.CreateMessageResponse(msg, data, status);
            await this.SendAsync(responseMsg);
        }

        private void HandleResponseMessage(Message msg)
            => this.MessageResponseReceived?.Invoke(msg);

        private async Task HandleReceivedMessageAsync(Message msg)
        {
            switch (msg.Identifier)
            {
                case MessageConstants.REGISTER_IDENTIFIER:
                    await this.HandleUpdateProcessMessageAsync(msg, true);
                    break;
                case MessageConstants.TERMINATE_IDENTIFIER:
                    await this.HandleUpdateProcessMessageAsync(msg, false);
                    break;
                case MessageConstants.REQUEST_PROCESSES_INFO_IDENTIFIER:
                    this.HandleRequestProcessesMessage(msg);
                    break;
                default:
                    switch (msg.Type)
                    {
                        case MessageType.Request:
                            await this.SendResponseMessageAsync(msg);
                            break;
                        case MessageType.Response:
                            this.HandleResponseMessage(msg);
                            break;
                        case MessageType.Unknown:
                        default:
                            this.LogEvent(LogSeverity.Warn, $"Received unknown message type\n{msg.Data}");
                            break;
                    }
                    break;
            }
        }

        private TcpState GetTcpState()
        {
            TcpConnectionInformation conInfo = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpConnections()
                .SingleOrDefault(con => con.LocalEndPoint.Equals(this.Client.Client.LocalEndPoint));

            return conInfo != null ? conInfo.State : TcpState.Unknown;
        }

        private async Task ListenAsync()
        {
            NetworkStream stream = this.Stream;
            try
            {
                while (this.IsConnected)
                {
                    int bytesRead = await stream.ReadAsync(this.Buffer, 0, this.Config.BufferSize);
                    if (bytesRead <= 0)
                    {
                        TcpState tcpState = this.GetTcpState();
                        if (tcpState != TcpState.Established)
                        {
                            await this.DisconnectAsync();
                            return;
                        }

                        this.ClearBuffer();
                        await Task.Delay(10);
                        continue;
                    }

                    string data = Encoding.UTF8.GetString(this.Buffer, 0, bytesRead);
                    this.ClearBuffer();
                    this.LogEvent(LogSeverity.Debug, $"Buffer contains {data.Length} bytes");
                    List<Message> messages = this.Reader.Read(data);

                    foreach (Message msg in messages)
                        await this.HandleReceivedMessageAsync(msg);
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is IOException)
            {
                await this.DisconnectAsync();
            }
            catch(Exception ex)
            {
                this.LogEvent(LogSeverity.Error, $"Please report the following exception to https://github.com/Earu/Octovisor/issues\n {ex.ToString()}");
            }
        }

        private Task<T> WaitInternalResponseAsync<T>(TaskCompletionSource<T> tcs)
        {
            CancellationTokenSource cts = new CancellationTokenSource(this.Config.Timeout);
            cts.Token.Register(() =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.SetException(new TimeOutException());
            });

            return tcs.Task;
        }

        private async Task RegisterAsync()
        {
            this.RegisterTCS = new TaskCompletionSource<bool>();
            Task<bool> t = this.WaitInternalResponseAsync(this.RegisterTCS); 
            await this.SendAsync(this.MessageFactory.CreateClientRegisterMessage(this.Config.ProcessName, this.Config.Token));
            this.LogEvent(LogSeverity.Info, "Registering on server");

            bool accepted = await t;
            this.RegisterTCS = null;

            if (accepted)
            {
                this.IsRegisteredInternal = true;
                await this.RequestProcessesInfoAsync();
            }
        }

        private async Task UnregisterAsync()
        {
            this.UnregisterTCS = new TaskCompletionSource<bool>();
            Task<bool> t = this.WaitInternalResponseAsync(this.UnregisterTCS);
            await this.SendAsync(this.MessageFactory.CreateClientUnregisterMessage(this.Config.ProcessName, this.Config.Token));
            this.LogEvent(LogSeverity.Info, "Ending on server");

            bool accepted = await t;
            this.UnregisterTCS = null;

            if (accepted)
                this.IsRegisteredInternal = false;
        }

        private async Task RequestProcessesInfoAsync()
        { 
            this.RequestProcessesInfoTCS = new TaskCompletionSource<List<RemoteProcessData>>();
            Task<List<RemoteProcessData>> t = this.WaitInternalResponseAsync(this.RequestProcessesInfoTCS);
            await this.SendAsync(this.MessageFactory.CreateClientRequestProcessesInfoMessage(this.Config.ProcessName));
            this.LogEvent(LogSeverity.Info, "Requesting available processes information");

            List<RemoteProcessData> data = await t;
            this.RequestProcessesInfoTCS = null;

            this.ProcessesInfoReceived?.Invoke(data);
        }

        internal async Task SendAsync(Message msg)
        {
            if (!this.IsConnectedInternal)
                throw new NotConnectedException();

            string data = msg.Serialize() + this.Config.MessageFinalizer;
            byte[] bytedata = Encoding.UTF8.GetBytes(data);

            this.LogEvent(LogSeverity.Debug, $"Sending {data.Length} bytes");
            NetworkStream stream = this.Stream;
            await stream.WriteAsync(bytedata, 0, bytedata.Length);
            await stream.FlushAsync();
        }
    }
}
