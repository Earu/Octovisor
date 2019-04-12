using Octovisor.Client.Exceptions;
using Octovisor.Messages;
using System;
using System.Collections.Generic;
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
        private int CurrentMessageID = 0;

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

        internal event Action<ProcessUpdateData, bool> ProcessUpdate;

        internal event Action<List<RemoteProcessData>> ProcessesInfoReceived;

        internal event Func<Message, string> MessageReceived;
       
        private TcpClient Client;

        private readonly byte[] Buffer;
        private readonly Config Config;
        private readonly MessageReader Reader;
        private readonly Thread ReceivingThread;

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
            this.ReceivingThread = new Thread(async () => await this.ListenAsync());
            this.MessageFactory = new MessageFactory();
            this.IsRegistered = false;
            this.IsConnected = false;
        }

        private void LogEvent(string log) => this.Log?.Invoke(log);

        /// <summary>
        /// Connects asynchronously to the Octovisor server
        /// </summary>
        public async Task ConnectAsync()
        {
            this.Client = new TcpClient();
            await this.Client.ConnectAsync(this.Config.Address, this.Config.Port);
            this.IsConnected = true;
            await this.Connected?.Invoke();
            this.ReceivingThread.Start();

            await this.RegisterAsync();
            await this.Registered?.Invoke();
        }

        /// <summary>
        /// Connects synchronously to the Octovisor server
        /// </summary>
        public void Connect()
        {
            Task t = this.ConnectAsync();
            t.Wait();
        }

        /// <summary>
        /// Disconnects asynchronously from the Octovisor server
        /// </summary>
        public async Task DisconnectAsync()
        {
            await this.UnregisterAsync();
            this.ReceivingThread.Abort();
            this.Client.Dispose();
            this.IsConnected = false;
        }

        /// <summary>
        /// Disconnects synchronously from the Octovisor server
        /// </summary>
        public void Disconnect()
        {
            Task t = this.DisconnectAsync();
            t.Wait();
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
            ProcessUpdateData updateData = msg.GetData<ProcessUpdateData>();
            this.ProcessUpdate?.Invoke(updateData, isRegisterUpdate);
            this.CompleteProcessUpdateTCS(updateData, isRegisterUpdate ? this.RegisterTCS : this.UnregisterTCS);
        }

        private async Task ListenAsync()
        {
            NetworkStream stream = this.Client.GetStream();
            while(this.IsConnected)
            {
                int bytesread = await stream.ReadAsync(this.Buffer, 0, this.Config.BufferSize);
                if (bytesread <= 0) continue;
                
                string data = Encoding.UTF8.GetString(this.Buffer);
                List<Message> messages = this.Reader.Read(data);
                this.ClearBuffer();

                foreach (Message msg in messages)
                {
                    switch(msg.Identifier)
                    {
                        case MessageConstants.REGISTER_IDENTIFIER:
                            this.HandleUpdateProcessMessage(msg, true);
                            break;
                        case MessageConstants.END_IDENTIFIER:
                            this.HandleUpdateProcessMessage(msg, false);
                            break;
                        case MessageConstants.REQUEST_PROCESSES_INFO_IDENTIFIER:
                            List<RemoteProcessData> processesData = msg.GetData<List<RemoteProcessData>>();
                            this.RequestProcessesInfoTCS?.SetResult(processesData);
                            break;
                        default:
                            this.MessageReceived?.Invoke(msg);
                            break;
                    }
                }
            }
        }

        private TaskCompletionSource<bool> RegisterTCS;
        private async Task RegisterAsync()
        {
            await this.SendAsync(this.MessageFactory.CreateRegisterMessage(this.Config.ProcessName, this.Config.Token));
            this.LogEvent("Registering on server");

            this.RegisterTCS = new TaskCompletionSource<bool>();
            CancellationTokenSource cts = new CancellationTokenSource(5000);
            cts.Token.Register(() => this.RegisterTCS?.SetCanceled(), false);
            bool accepted = await this.RegisterTCS.Task;
            if (accepted)
                this.IsRegistered = true;
            this.RegisterTCS = null;
        }

        private TaskCompletionSource<bool> UnregisterTCS;
        private async Task UnregisterAsync()
        {
            await this.SendAsync(this.MessageFactory.CreateUnregisterMessage(this.Config.ProcessName, this.Config.Token));
            this.LogEvent("Ending on server");

            this.UnregisterTCS = new TaskCompletionSource<bool>();
            CancellationTokenSource cts = new CancellationTokenSource(5000);
            cts.Token.Register(() => this.UnregisterTCS?.SetCanceled(), false);
            bool accepted = await this.UnregisterTCS.Task;
            if (accepted)
                this.IsRegistered = false;
            this.UnregisterTCS = null;
        }

        private TaskCompletionSource<List<RemoteProcessData>> RequestProcessesInfoTCS;
        private async Task RequestProcessesInfoAsync()
        {
            await this.SendAsync(this.MessageFactory.CreateRequestProcessesInfoMessage(this.Config.ProcessName));
            this.LogEvent("Requesting available processes information");

            this.RequestProcessesInfoTCS = new TaskCompletionSource<List<RemoteProcessData>>();
            CancellationTokenSource cts = new CancellationTokenSource(5000);
            cts.Token.Register(() => this.RequestProcessesInfoTCS?.SetCanceled(), false);
            List<RemoteProcessData> data = await this.RequestProcessesInfoTCS.Task;
            this.RequestProcessesInfoTCS = null;
            this.ProcessesInfoReceived?.Invoke(data);
        }

        internal async Task SendAsync(Message msg)
        {
            if (!this.IsConnected)
                throw new UnconnectedException();

            msg.ID = this.CurrentMessageID;
            Interlocked.Increment(ref this.CurrentMessageID);
            string data = $"{msg.Serialize()}{this.Config.MessageFinalizer}";
            byte[] bytedata = Encoding.UTF8.GetBytes(data);

            this.LogEvent($"Sending {data.Length} bytes\n{data}");

            NetworkStream stream = this.Client.GetStream();
            await stream.WriteAsync(bytedata, 0, bytedata.Length);
        }
    }
}
