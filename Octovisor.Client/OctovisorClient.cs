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
        private static readonly string MessageFinalizer = "\0";
        private static readonly int BufferSize = 256;
        private double CurrentMessageID = 0;

        public event Action<Exception> OnError;
        public event Action<string> Log;
       
        private TcpClient Client;

        private readonly ClientConfig Config;
        private readonly Dictionary<double, MessageHandle> MessageCallbacks;
        private readonly StringBuilder Builder;
        private readonly byte[] Buffer;

        public bool IsConnected { get; private set; }
        public bool IsRegistered { get; private set; }

        public OctovisorClient(ClientConfig config)
        {
            if (!config.IsValid())
                throw new Exception("Invalid Octovisor client configuration");

            this.Config = config;
            this.MessageCallbacks = new Dictionary<double, MessageHandle>();
            this.Builder = new StringBuilder();
            this.Buffer = new byte[BufferSize];
        }

        private void CallErrorEvent(Exception e) => this.OnError?.Invoke(e);
        private void CallLogEvent(string log)    => this.Log?.Invoke(log);

        /// <summary>
        /// Connects to the remote octovisor server
        /// </summary>
        public async Task Connect()
            => await this.InternalConnect().ConfigureAwait(false);

        private async Task InternalConnect()
        {
            try
            {
                this.Client = new TcpClient();
                await this.Client.ConnectAsync(this.Config.ServerAddress,this.Config.ServerPort);
                this.IsConnected = true;

                await this.Register();
            }
            catch(Exception e)
            {
                this.IsConnected = false;
                this.CallErrorEvent(e);
            }
        }

        private List<string> HandleReceivedData(string data)
        {
            List<string> msgdata = new List<string>();
            foreach (char c in data)
            {
                this.Builder.Append(c);

                string current = this.Builder.ToString();
                int endlen = MessageFinalizer.Length;
                if (current.Length >= endlen && current.Substring(current.Length - endlen, endlen) == MessageFinalizer)
                {
                    msgdata.Add(this.Builder.ToString());
                    this.Builder.Clear();
                }
            }

            return msgdata;
        }

        private async Task Receive()
        {
            NetworkStream stream = this.Client.GetStream();
            while(this.IsConnected)
            {
                int bytesread = await stream.ReadAsync(this.Buffer, 0, BufferSize);
                if(bytesread > 0)
                {
                    string data = Encoding.UTF8.GetString(this.Buffer);
                    List<Message> messages = this.HandleReceivedData(data)
                        .Select(x => Message.Deserialize(x))
                        .ToList();

                    foreach(Message msg in messages)
                    {
                        if(this.MessageCallbacks.ContainsKey(msg.ID))
                        {
                            MessageHandle handle = this.MessageCallbacks[msg.ID];
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
                OriginName = this.Config.ProcessName,
                TargetName = "SERVER",
                Identifier = "INTERNAL_OCTOVISOR_PROCESS_INIT",
                Data       = this.Config.Token,
                Status     = MessageStatus.DataRequest,
            });

            this.CallLogEvent("Registering on server");
        }

        private async Task Unregister()
        {
            await this.Send(new Message
            {
                OriginName = this.Config.ProcessName,
                TargetName = "SERVER",
                Identifier = "INTERNAL_OCTOVISOR_PROCESS_END",
                Data = this.Config.Token,
                Status = MessageStatus.DataRequest,
            });

            this.CallLogEvent("Ending on server");
        }

        public async Task Send(Message msg)
        {
            if (!this.IsConnected) return;
            //TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            try
            {
                msg.ID = this.CurrentMessageID++;
                string data = msg.Serialize();
                data += MessageFinalizer;
                byte[] bytedata = Encoding.UTF8.GetBytes(data);

                this.CallLogEvent($"Sending {data.Length} bytes\n{data}");
                //this.MessageCallbacks[msg.ID] = new MessageHandle(typeof(T),tcs);

                NetworkStream stream = this.Client.GetStream();
                await stream.WriteAsync(bytedata, 0, bytedata.Length);
                await stream.FlushAsync();
            }
            catch(Exception e)
            {
                //tcs.SetException(e);
                this.CallErrorEvent(e);
            }

            //await tcs.Task;
        }
    }
}
