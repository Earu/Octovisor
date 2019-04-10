using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octovisor.Models;

namespace Octovisor.Client
{
    public class OctovisorClient
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
       
        private TcpClient _Client;
        private byte[] _Buffer;

        private readonly Config _Config;
        private readonly MessageReader _Reader;
        private readonly MessageFactory _MessageFactory;
        private readonly Thread _ReceivingThread;

        /// <summary>
        /// Gets whether or not this instance is connected 
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Gets whether or not this instance is registered
        /// </summary>
        public bool IsRegistered { get; private set; }

        /// <summary>
        /// Creates a new instance of OctovisorClient
        /// </summary>
        /// <param name="config">A config object containing your token and other settings</param>
        public OctovisorClient(Config config)
        {
            if (!config.IsValid())
                throw new Exception("Invalid Octovisor client configuration");

            this._Config = config;
            this._Reader = new MessageReader(config.MessageFinalizer);
            this._Buffer = new byte[config.BufferSize];
            this._MessageFactory = new MessageFactory();
            this._ReceivingThread = new Thread(async () => await this.Receive()); 
        }

        private void ExceptionEvent(Exception e) => this.ExceptionThrown?.Invoke(e);
        private void LogEvent(string log) => this.Log?.Invoke(log);

        /// <summary>
        /// Connects to the remote octovisor server
        /// </summary>
        public async Task Connect()
            => await this.InternalConnect().ConfigureAwait(false);

        private async Task InternalConnect()
        {
            try
            {
                this._Client = new TcpClient();
                await this._Client.ConnectAsync(this._Config.Address, this._Config.Port);
                this.IsConnected = true;

                await this.Register();
                this._ReceivingThread.Start();
            }
            catch(Exception e)
            {
                this.IsConnected = false;
                this.ExceptionEvent(e);
            }
        }

        private List<Message> HandleReceivedData(string data)
            => this._Reader.Read(data);

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
                List<Message> messages = this.HandleReceivedData(data);
                foreach(Message msg in messages)
                {
                    this.LogEvent($"{msg.ID} | {msg.Identifier} | {msg.Status}");
                    if (msg.Status == MessageStatus.MalformedMessageError)
                        this.LogEvent(msg.Data);
                }

                this.ClearBuffer();
            }
        }

        private async Task Register()
        {
            await this.Send(this._MessageFactory.CreateRegisterMessage(this._Config.ProcessName, this._Config.Token));
            this.LogEvent("Registering on server");
        }

        private async Task Unregister()
        {
            await this.Send(this._MessageFactory.CreateUnregisterMessage(this._Config.ProcessName, this._Config.Token));
            this.LogEvent("Ending on server");
        }

        //to switch to private / internal
        public async Task Send(Message msg)
        {
            if (!this.IsConnected) return;
            //TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            try
            {
                msg.ID = this._CurrentMessageID;
                Interlocked.Increment(ref this._CurrentMessageID);
                string data = $"{msg.Serialize()}{this._Config.MessageFinalizer}";
                byte[] bytedata = Encoding.UTF8.GetBytes(data);

                this.LogEvent($"Sending {data.Length} bytes\n{data}");
                //this.MessageCallbacks[msg.ID] = new MessageHandle(typeof(T),tcs);

                NetworkStream stream = this._Client.GetStream();
                await stream.WriteAsync(bytedata, 0, bytedata.Length);
            }
            catch(Exception e)
            {
                //tcs.SetException(e);
                this.ExceptionEvent(e);
            }

            //await tcs.Task;
        }
    }
}
