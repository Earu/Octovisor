using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octovisor.Models;

namespace Octovisor.Client
{
    public class OctovisorClient
    {
        private static readonly string MessageFinalizer = "__END__";
        private double CurrentMessageID = 0;

        public event Action<Exception> OnError;
        public event Action<string> Log;

        private TcpClient Client;

        private readonly ClientConfig Config;

        public bool IsConnected { get; private set; }
        public bool IsRegistered { get; private set; }

        public OctovisorClient(ClientConfig config)
        {
            if (!config.IsValid())
                throw new Exception("Invalid Octovisor client configuration");
                
            this.Config = config;
            this.Client = new TcpClient();
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
                IPHostEntry hostinfo = Dns.GetHostEntry(this.Config.ServerAddress);
                IPAddress ipadr      = hostinfo.AddressList[0];
                
                await this.Client.ConnectAsync(ipadr,this.Config.ServerPort);
                this.IsConnected = true;

                await this.Register();
            }
            catch(Exception e)
            {
                this.IsConnected = false;
                this.CallErrorEvent(e);
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
                Data       = this.Config.Token,
                Status     = MessageStatus.DataRequest,
            });

            this.CallLogEvent("Ending on server");
        }

        public async Task Send(Message msg)
        {
            if (!this.IsConnected) return;

            try
            {
                msg.ID = this.CurrentMessageID++;
                string data = msg.Serialize();
                data += MessageFinalizer;
                byte[] bytedata = Encoding.UTF8.GetBytes(data);

                this.CallLogEvent($"Sending {data.Length} bytes\n{data}");

                NetworkStream stream = this.Client.GetStream();
                await stream.WriteAsync(bytedata,0,bytedata.Length);
                await stream.FlushAsync();
            }
            catch(Exception e)
            {
                this.CallErrorEvent(e);
            }
        }
    }
}
