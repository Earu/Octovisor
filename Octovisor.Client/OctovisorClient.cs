using Octovisor.Client.Models;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Octovisor.Client
{
    public class OctovisorClient
    {
        private readonly ManualResetEvent OnConnectDone;
        private readonly ManualResetEvent OnSendDone;
        private readonly ManualResetEvent OnReceiveDone;

        private Socket Client;
        private string Response;

        internal readonly OctovisorConfig Config;

        public bool IsConnected { get; private set; }

        public OctovisorClient(OctovisorConfig config)
        {
            if (!config.IsValid)
                throw new Exception("Invalid Octovisor config");

            this.Response      = string.Empty;
            this.Config        = config;
            this.OnConnectDone = new ManualResetEvent(false);
            this.OnSendDone    = new ManualResetEvent(false);
            this.OnReceiveDone = new ManualResetEvent(false);

            this.Connect();
        }

        /// <summary>
        /// This is called into the constructor so do not use it again unless you get disconnected
        /// </summary>
        public void Connect()
        {
            try
            {
                IPHostEntry hostinfo = Dns.GetHostEntry(this.Config.ServerAddress);
                IPAddress ipadr = hostinfo.AddressList[0];
                IPEndPoint endpoint = new IPEndPoint(ipadr, this.Config.ServerPort);

                this.Client = new Socket(ipadr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                this.Client.BeginConnect(endpoint, new AsyncCallback(this.ConnectCallback), this.Client);
                this.OnConnectDone.WaitOne();

                this.RegisterOnServer();

                this.IsConnected = true;
            }
            catch
            {
                this.IsConnected = false;
            }
        }

        internal void RegisterOnServer()
        {
            ProcessMessage msg = new ProcessMessage {
                OriginName = this.Config.ProcessName,
                TargetName = "SERVER",
                MessageIdentifier = "INTERNAL_OCTOVISOR_PROCESS_INIT",
                Data = null,
            };

            this.Send(msg.Serialize());
        }

        internal void EndOnServer()
        {
            ProcessMessage msg = new ProcessMessage
            {
                OriginName = this.Config.ProcessName,
                TargetName = "SERVER",
                MessageIdentifier = "INTERNAL_OCTOVISOR_PROCESS_END",
                Data = null,
            };

            this.Send(msg.Serialize());
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndConnect(ar);

                this.OnConnectDone.Set();
            }
            catch
            {
                
            }
        }

        internal string Receive()
        {
            try
            {
                StateObject state = new StateObject
                {
                    WorkSocket = this.Client
                };

                this.Client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(this.ReceiveCallback), state);

                this.OnReceiveDone.WaitOne();

                return Response;
            }
            catch 
            {
                return null;
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
                    state.Builder.Append(Encoding.UTF8.GetString(state.Buffer, 0, bytesread));

                    client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(this.ReceiveCallback), state);
                }
                else
                {
                    if (state.Builder.Length > 1)
                        Response = state.Builder.ToString();

                    this.OnReceiveDone.Set();
                }
            }
            catch 
            {
            }
        }

        internal void Send(string data)
        {
            byte[] bytedata = Encoding.UTF8.GetBytes(data);

            this.Client.BeginSend(bytedata, 0, bytedata.Length, 0, new AsyncCallback(this.SendCallback), this.Client);

            this.OnSendDone.WaitOne();
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;

                client.EndSend(ar);

                this.OnSendDone.Set();
            }
            catch 
            {
            }
        }

        public RemoteProcess ListenToProcess(string process)
            => new RemoteProcess(this, process);
    }
}
