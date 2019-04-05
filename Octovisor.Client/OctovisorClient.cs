using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Octovisor.Models;

namespace Octovisor.Client
{
    public class OctovisorClient
    {
        private double _CurrentMessageID = 0;

        /// <summary>
        /// Fired whenever an exception is thrown
        /// </summary>
        public event Action<Exception> ExceptionThrown;

        /// <summary>
        /// Fired when something is logged
        /// </summary>
        public event Action<string> Log;
       
        private TcpClient _Client;

        private readonly Config _Config;
        private readonly Dictionary<double, MessageHandle> _MessageCallbacks;
        private readonly StringBuilder _Builder;
        private readonly byte[] _Buffer;

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
            this._MessageCallbacks = new Dictionary<double, MessageHandle>();
            this._Builder = new StringBuilder();
            this._Buffer = new byte[config.BufferSize];
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
            }
            catch(Exception e)
            {
                this.IsConnected = false;
                this.ExceptionEvent(e);
            }
        }

        private List<Message> HandleReceivedData(string data)
        {
            List<string> msgdata = new List<string>();
            foreach (char c in data)
            {
                this._Builder.Append(c);

                string current = this._Builder.ToString();
                int endlen = this._Config.MessageFinalizer.Length;
                if (current.Length >= endlen && current.Substring(current.Length - endlen, endlen) == this._Config.MessageFinalizer)
                {
                    msgdata.Add(this._Builder.ToString());
                    this._Builder.Clear();
                }
            }

            return msgdata.Select(Message.Deserialize).ToList();
        }

        private async Task Receive()
        {
            NetworkStream stream = this._Client.GetStream();
            while(this.IsConnected)
            {
                int bytesread = await stream.ReadAsync(this._Buffer, 0, this._Config.BufferSize);
                if(bytesread > 0)
                {
                    string data = Encoding.UTF8.GetString(this._Buffer);
                    List<Message> messages = this.HandleReceivedData(data);
                    foreach(Message msg in messages)
                    {
                        if(this._MessageCallbacks.ContainsKey(msg.ID))
                        {
                            MessageHandle handle = this._MessageCallbacks[msg.ID];
                            if (msg.Status != MessageStatus.DataRequest)
                            {
                                
                            }
                            else
                            {

                            }
                        }
                    }
                }
            }
        }

        private async Task Register()
        {
            await this.Send(new Message
            {
                OriginName = this._Config.ProcessName,
                TargetName = "SERVER",
                Identifier = "INTERNAL_OCTOVISOR_PROCESS_INIT",
                Data       = this._Config.Token,
                Status     = MessageStatus.DataRequest,
            });

            this.LogEvent("Registering on server");
        }

        private async Task Unregister()
        {
            await this.Send(new Message
            {
                OriginName = this._Config.ProcessName,
                TargetName = "SERVER",
                Identifier = "INTERNAL_OCTOVISOR_PROCESS_END",
                Data = this._Config.Token,
                Status = MessageStatus.DataRequest,
            });

            this.LogEvent("Ending on server");
        }

        //to switch to private / internal
        public async Task Send(Message msg)
        {
            if (!this.IsConnected) return;
            //TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            try
            {
                msg.ID = this._CurrentMessageID++;
                string data = msg.Serialize();
                data += this._Config.MessageFinalizer;
                byte[] bytedata = Encoding.UTF8.GetBytes(data);

                this.LogEvent($"Sending {data.Length} bytes\n{data}");
                //this.MessageCallbacks[msg.ID] = new MessageHandle(typeof(T),tcs);

                NetworkStream stream = this._Client.GetStream();
                await stream.WriteAsync(bytedata, 0, bytedata.Length);
                await stream.FlushAsync();
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
