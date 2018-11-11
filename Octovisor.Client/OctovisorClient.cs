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

        public event Action<Exception> OnError;
        public event Action<string> Log;

        private readonly ManualResetEvent OnConnectDone;
        private readonly ManualResetEvent OnSendDone;
        private readonly ManualResetEvent OnReceiveDone;

        private Socket Client;

        private readonly ClientConfig Config;

        public bool IsConnected { get; private set; }

        public OctovisorClient(ClientConfig config)
        {
            if (!config.IsValid())
                throw new Exception("Invalid Octovisor client configuration");
                
            this.Config        = config;
            this.OnConnectDone = new ManualResetEvent(false);
            this.OnSendDone    = new ManualResetEvent(false);
            this.OnReceiveDone = new ManualResetEvent(false);
        }

        private void CallErrorEvent(Exception e) => this.OnError?.Invoke(e);
        private void CallLogEvent(string log) => this.Log?.Invoke(log);

        /// <summary>
        /// Connects to the remote octovisor server
        /// </summary>
        public async Task Connect()
            => await this.InternalConnect().ConfigureAwait(false);

        private async Task InternalConnect()
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            try
            {
                IPHostEntry hostinfo = Dns.GetHostEntry(this.Config.ServerAddress);
                IPAddress ipadr      = hostinfo.AddressList[0];
                IPEndPoint endpoint  = new IPEndPoint(ipadr, this.Config.ServerPort);
                
                this.Client = new Socket(ipadr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                this.Client.BeginConnect(endpoint, res => {
                    this.ConnectCallback(res);
                    tcs.SetResult(true);
                    this.IsConnected = true;
                }, this.Client);
                this.OnConnectDone.WaitOne();
            }
            catch(Exception e)
            {
                this.IsConnected = false;
                this.CallErrorEvent(e);
            }

            await tcs.Task;

            if(this.IsConnected)
            {
                await this.RegisterOnServer();
                this.StartReceiving();
            }
        }

        private async Task RegisterOnServer()
        {
            await this.Send(new Message
            {
                OriginName = this.Config.ProcessName,
                TargetName = "SERVER",
                Identifier = "INTERNAL_OCTOVISOR_PROCESS_INIT",
                Data = this.Config.Token,
                Status = MessageStatus.OK,
            });

            this.CallLogEvent("Registering on server");
        }

        private async Task EndOnServer()
        {
            await this.Send(new Message
            {
                OriginName = this.Config.ProcessName,
                TargetName = "SERVER",
                Identifier = "INTERNAL_OCTOVISOR_PROCESS_END",
                Data = this.Config.Token,
                Status = MessageStatus.OK,
            });

            this.CallLogEvent("Ending on server");
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndConnect(ar);

                this.OnConnectDone.Set();
            }
            catch(Exception e)
            {
                this.CallErrorEvent(e);
            }
        }

        private void StartReceiving()
        {
            this.CallLogEvent("Beginning receiving from server");

            try
            {
                StateObject state = new StateObject(this.Client);

                this.Client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, this.ReceiveCallback, state);
            }
            catch(Exception e) 
            {
                this.CallErrorEvent(e);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.WorkSocket;

                int bytesread = client.EndReceive(ar);

                if (bytesread > 0)
                {
                    string data = Encoding.UTF8.GetString(state.Buffer, 0, bytesread);
                    this.CallLogEvent($"Received {bytesread} bytes");

                    client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, this.ReceiveCallback, state);
                }
                else
                {
                    this.OnReceiveDone.Set();
                }
            }
            catch (Exception e)
            {
                this.CallErrorEvent(e);
            }
        }

        private Task Send(Message msg)
        {
            if (!this.IsConnected) return Task.CompletedTask;

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            try
            {
                string data = msg.Serialize();
                data += MessageFinalizer;
                byte[] bytedata = Encoding.UTF8.GetBytes(data);

                this.CallLogEvent($"Sending {data.Length} bytes");
                this.Client.BeginSend(bytedata, 0, bytedata.Length, 0, res =>
                {
                    this.SendCallback(res);
                    tcs.SetResult(true);
                }, this.Client);
                this.OnSendDone.WaitOne();
            }
            catch(Exception e)
            {
                this.CallErrorEvent(e);
                tcs.SetException(e);
            }

            return tcs.Task;
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndSend(ar);

                this.OnSendDone.Set();
            }
            catch(Exception e)
            {
                this.CallErrorEvent(e);
            }
        }

        public async Task SendGarbage(string proc,string msg)
        {
            await this.Send(new Message
            {
                ID = 666,
                TargetName = proc,
                Identifier = msg,
                OriginName = this.Config.ProcessName,
                Data = null,
                Status = MessageStatus.OK,
            });
        }
    }
}
