using Octovisor.Client.Exceptions;
using Octovisor.Messages;
using System;
using System.Collections.Generic;
using System.IO;
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
        private TcpClient Client;
        private Task ReceivingTask;
        private NetworkStream Stream;

        private readonly byte[] Buffer;
        private readonly Config Config;
        private readonly MessageReader Reader;
        private volatile TaskCompletionSource<bool> RegisterTCS;
        private volatile TaskCompletionSource<bool> UnregisterTCS;
        private volatile TaskCompletionSource<List<RemoteProcessData>> RequestProcessesInfoTCS;

        /// <summary>
        /// Fired when something is logged
        /// </summary>
        public event Action<string> Log;

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
       
        /// <summary>
        /// Gets whether or not this instance is connected 
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Gets whether or not this instance is registered
        /// </summary>
        public bool IsRegistered { get; private set; }

        internal MessageFactory MessageFactory { get; private set; }

        /// <summary>
        /// Creates a new instance of OctovisorClient
        /// </summary>
        /// <param name="config">A config object containing your token and other settings</param>
        public BaseClient(Config config)
        {
            if (!config.IsValid())
                throw new Exception("Invalid Octovisor client configuration");

            this.Config = config;
            this.Reader = new MessageReader(config.MessageFinalizer);
            this.Buffer = new byte[config.BufferSize];
            this.MessageFactory = new MessageFactory(config.CompressionTreshold);
            this.IsRegistered = false;
            this.IsConnected = false;
        }

        private void LogEvent(string log) => this.Log?.Invoke(log);

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
            if (this.Registered != null)
                await this.Registered.Invoke();
        }

        /// <summary>
        /// Disconnects asynchronously from the Octovisor server
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (!this.IsConnected)
                throw new UnconnectedException();

            if (this.Client.Connected) // socket connected
                await this.UnregisterAsync();

            this.Stream.Dispose();
            this.Client.Dispose();
            this.IsConnected = false;

            await this.ReceivingTask;
            this.ReceivingTask = null;

            if (this.Disconnected != null)
                await this.Disconnected.Invoke();
        }

        private void ClearBuffer()
            => Array.Clear(this.Buffer, 0, this.Buffer.Length);

        private void CompleteProcessUpdateTCS(ProcessUpdateData updateData, TaskCompletionSource<bool> tcs)
        {
            if (updateData.Accepted && updateData.Name.Equals(this.Config.ProcessName))
                tcs?.SetResult(updateData.Accepted);
        }

        private void HandleUpdateProcessMessage(Message msg, bool isRegisterUpdate)
        {
            if (msg.HasException) return; //timeout

            ProcessUpdateData updateData = msg.GetData<ProcessUpdateData>();
            this.CompleteProcessUpdateTCS(updateData, isRegisterUpdate ? this.RegisterTCS : this.UnregisterTCS);

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
                    this.HandleUpdateProcessMessage(msg, true);
                    break;
                case MessageConstants.TERMINATE_IDENTIFIER:
                    this.HandleUpdateProcessMessage(msg, false);
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
                            this.LogEvent($"Received unknown message type\n{msg.Data}");
                            break;
                    }
                    break;
            }
        }

        private async Task ListenAsync()
        {
            NetworkStream stream = this.Stream;
            try
            {
                while (this.IsConnected)
                {
                    int bytesread = await stream.ReadAsync(this.Buffer, 0, this.Config.BufferSize);
                    if (bytesread <= 0) continue;

                    string data = Encoding.UTF8.GetString(this.Buffer);
                    List<Message> messages = this.Reader.Read(data);
                    this.ClearBuffer();

                    foreach (Message msg in messages)
                        await this.HandleReceivedMessageAsync(msg);
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is IOException)
            {
                await this.DisconnectAsync();
            }
        }

        private async Task<T> WaitInternalResponseAsync<T>(TaskCompletionSource<T> tcs)
        {
            CancellationTokenSource cts = new CancellationTokenSource(5000);
            cts.Token.Register(() =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.SetException(new TimeOutException());
            }, false);

            return await tcs.Task;
        }

        private async Task RegisterAsync()
        {
            await this.SendAsync(this.MessageFactory.CreateClientRegisterMessage(this.Config.ProcessName, this.Config.Token));
            this.LogEvent("Registering on server");

            this.RegisterTCS = new TaskCompletionSource<bool>();
            bool accepted = await this.WaitInternalResponseAsync(this.RegisterTCS);
            this.RegisterTCS = null;

            if (accepted)
                this.IsRegistered = true;

            await this.RequestProcessesInfoAsync();
        }

        private async Task UnregisterAsync()
        {
            await this.SendAsync(this.MessageFactory.CreateClientUnregisterMessage(this.Config.ProcessName, this.Config.Token));
            this.LogEvent("Ending on server");

            this.UnregisterTCS = new TaskCompletionSource<bool>();
            bool accepted = await this.WaitInternalResponseAsync(this.UnregisterTCS);
            this.UnregisterTCS = null;

            if (accepted)
                this.IsRegistered = false;
        }

        private async Task RequestProcessesInfoAsync()
        {
            await this.SendAsync(this.MessageFactory.CreateClientRequestProcessesInfoMessage(this.Config.ProcessName));
            this.LogEvent("Requesting available processes information");

            this.RequestProcessesInfoTCS = new TaskCompletionSource<List<RemoteProcessData>>();
            List<RemoteProcessData> data = await this.WaitInternalResponseAsync(this.RequestProcessesInfoTCS);
            this.RequestProcessesInfoTCS = null;

            this.ProcessesInfoReceived?.Invoke(data);
        }

        internal async Task SendAsync(Message msg)
        {
            if (!this.IsConnected)
                throw new UnconnectedException();

            string data = msg.Serialize() + this.Config.MessageFinalizer;
            byte[] bytedata = Encoding.UTF8.GetBytes(data);

            this.LogEvent($"Sending {data.Length} bytes");
            NetworkStream stream = this.Stream;
            await stream.WriteAsync(bytedata, 0, bytedata.Length);
            await stream.FlushAsync();
        }
    }
}
