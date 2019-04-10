using Octovisor.Messages;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octovisor.Client
{
    public abstract class BaseClient
    {
        private int _CurrentMessageID = 0;

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
       
        private TcpClient _Client;

        private readonly byte[] _Buffer;
        private readonly Config _Config;
        private readonly MessageReader _Reader;
        private readonly Thread _ReceivingThread;

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

            this._Config = config;
            this._Reader = new MessageReader(config.MessageFinalizer);
            this._Buffer = new byte[config.BufferSize];
            this._ReceivingThread = new Thread(async () => await this.Receive());

            this.MessageFactory = new MessageFactory();
        }

        private void ExceptionEvent(Exception e) => this.ExceptionThrown?.Invoke(e);
        private void LogEvent(string log) => this.Log?.Invoke(log);

        /// <summary>
        /// Connects to the remote octovisor server
        /// </summary>
        public async Task ConnectAsync()
            => await this.InternalConnect().ConfigureAwait(false);

        private async Task InternalConnect()
        {
            try
            {
                this._Client = new TcpClient();
                await this._Client.ConnectAsync(this._Config.Address, this._Config.Port);
                this.IsConnected = true;
                this.Connected?.Invoke();

                await this.Register();
                this.Registered?.Invoke();
                this._ReceivingThread.Start();
            }
            catch(Exception e)
            {
                this.IsConnected = false;
                this.ExceptionEvent(e);
            }
        }

        private void ClearBuffer()
            => Array.Clear(this._Buffer, 0, this._Buffer.Length);

        private async Task Receive()
        {
            NetworkStream stream = this._Client.GetStream();
            while(this.IsConnected)
            {
                int bytesread = await stream.ReadAsync(this._Buffer, 0, this._Config.BufferSize);
                if (bytesread <= 0) continue;
                
                string data = Encoding.UTF8.GetString(this._Buffer);
                List<Message> messages = this._Reader.Read(data);
                this.ClearBuffer();

                foreach (Message msg in messages)
                {
                    this.LogEvent($"{msg.ID} | {msg.Identifier} | {msg.Status}");
                    if (msg.Status == MessageStatus.MalformedMessageError)
                        this.LogEvent(msg.Data);
                }
            }
        }

        private async Task Register()
        {
            await this.SendAsync(this.MessageFactory.CreateRegisterMessage(this._Config.ProcessName, this._Config.Token));
            this.LogEvent("Registering on server");
        }

        private async Task Unregister()
        {
            await this.SendAsync(this.MessageFactory.CreateUnregisterMessage(this._Config.ProcessName, this._Config.Token));
            this.LogEvent("Ending on server");
        }

        internal async Task SendAsync(Message msg)
        {
            if (!this.IsConnected) return;

            try
            {
                msg.ID = this._CurrentMessageID;
                Interlocked.Increment(ref this._CurrentMessageID);
                string data = $"{msg.Serialize()}{this._Config.MessageFinalizer}";
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
