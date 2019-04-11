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
        /// Fired whenever an exception is thrown
        /// </summary>
        public event Action<Exception> ExceptionThrown;

        /// <summary>
        /// Fired when something is logged
        /// </summary>
        public event Action<string> Log;

        /// <summary>
        /// Fired when the client is connected to the remote server
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// Fired when the client is registered on the remote server
        /// </summary>
        public event Action Registered;

        internal event Action<ProcessUpdateData, bool> ProcessUpdate;

        internal event Func<Message, string> MessageReceived;
       
        private TcpClient _Client;

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
        }

        private void ExceptionEvent(Exception e) => this.ExceptionThrown?.Invoke(e);
        private void LogEvent(string log) => this.Log?.Invoke(log);

        /// <summary>
        /// Connects to the remote octovisor server
        /// </summary>
        public async Task ConnectAsync()
            => await this.InternalConnectAsync().ConfigureAwait(false);

        private async Task InternalConnectAsync()
        {
            try
            {
                this._Client = new TcpClient();
                await this._Client.ConnectAsync(this.Config.Address, this.Config.Port);
                this.IsConnected = true;
                this.Connected?.Invoke();

                await this.RegisterAsync();
                this.Registered?.Invoke();
                this.ReceivingThread.Start();
            }
            catch(Exception e)
            {
                this.IsConnected = false;
                this.ExceptionEvent(e);
            }
        }

        private void ClearBuffer()
            => Array.Clear(this.Buffer, 0, this.Buffer.Length);

        private async Task ListenAsync()
        {
            NetworkStream stream = this._Client.GetStream();
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
                            ProcessUpdateData registerData = msg.GetData<ProcessUpdateData>();
                            this.ProcessUpdate?.Invoke(registerData, true);
                            break;
                        case MessageConstants.END_IDENTIFIER:
                            ProcessUpdateData endData = msg.GetData<ProcessUpdateData>();
                            this.ProcessUpdate?.Invoke(endData, false);
                            break;
                        default:
                            this.MessageReceived?.Invoke(msg);
                            break;
                    }
                }
            }
        }

        private async Task RegisterAsync()
        {
            await this.SendAsync(this.MessageFactory.CreateRegisterMessage(this.Config.ProcessName, this.Config.Token));
            this.LogEvent("Registering on server");
        }

        private async Task UnregisterAsync()
        {
            await this.SendAsync(this.MessageFactory.CreateUnregisterMessage(this.Config.ProcessName, this.Config.Token));
            this.LogEvent("Ending on server");
        }

        internal async Task SendAsync(Message msg)
        {
            if (!this.IsConnected) return;

            try
            {
                msg.ID = this.CurrentMessageID;
                Interlocked.Increment(ref this.CurrentMessageID);
                string data = $"{msg.Serialize()}{this.Config.MessageFinalizer}";
                byte[] bytedata = Encoding.UTF8.GetBytes(data);

                this.LogEvent($"Sending {data.Length} bytes\n{data}");

                NetworkStream stream = this._Client.GetStream();
                await stream.WriteAsync(bytedata, 0, bytedata.Length);
            }
            catch(Exception e)
            {
                this.ExceptionEvent(e);
            }
        }
    }
}
