using Octovisor.Client.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octovisor.Client
{
    public class OctovisorClient
    {
        public event Action<Exception> OnError;

        private readonly ManualResetEvent OnConnectDone;
        private readonly ManualResetEvent OnSendDone;
        private readonly ManualResetEvent OnReceiveDone;

        private Socket Client;
        private readonly Dictionary<string,Dictionary<ulong, TaskCompletionSource<ProcessMessage>>> ResponseQueue;

        internal readonly OctovisorConfig Config;

        public bool IsConnected { get; private set; }

        public OctovisorClient(OctovisorConfig config)
        {
            if (!config.IsValid)
                throw new Exception("Invalid Octovisor config");

            this.ResponseQueue = new Dictionary<string, Dictionary<ulong, TaskCompletionSource<ProcessMessage>>>();
            this.Config        = config;
            this.OnConnectDone = new ManualResetEvent(false);
            this.OnSendDone    = new ManualResetEvent(false);
            this.OnReceiveDone = new ManualResetEvent(false);

            this.Connect();
        }

        private void CallErrorEvent(Exception e) => this.OnError?.Invoke(e);

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
            catch(Exception e)
            {
                this.IsConnected = false;
                this.CallErrorEvent(e);
            }

            this.StartReceiving();
        }

        internal void RegisterOnServer()
        {
            this.Send(new ProcessMessage
            {
                OriginName = this.Config.ProcessName,
                TargetName = "SERVER",
                MessageIdentifier = "INTERNAL_OCTOVISOR_PROCESS_INIT",
                Data = null,
            });
        }

        internal void EndOnServer()
        {
            this.Send(new ProcessMessage
            {
                OriginName = this.Config.ProcessName,
                TargetName = "SERVER",
                MessageIdentifier = "INTERNAL_OCTOVISOR_PROCESS_END",
                Data = null,
            });
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
            if (!this.IsConnected) return;
            
            try
            {
                StateObject state = new StateObject(this.Client);

                this.Client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(this.ReceiveCallback), state);
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
                    ProcessMessage msg = ProcessMessage.Deserialize(data);
                    if(this.ResponseQueue.ContainsKey(msg.TargetName) && msg.OriginName == this.Config.ProcessName)
                    {
                        Dictionary<ulong,TaskCompletionSource<ProcessMessage>> processtcs = this.ResponseQueue[msg.TargetName];
                        if(processtcs.ContainsKey(msg.ID))
                        {
                            TaskCompletionSource<ProcessMessage> tsource = processtcs[msg.ID];
                            tsource.SetResult(msg);
                        }
                    }

                    client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(this.ReceiveCallback), state);
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

        internal void Send(ProcessMessage msg)
        {
            if (!this.IsConnected) return;

            if (!this.ResponseQueue.ContainsKey(msg.TargetName))
                this.ResponseQueue[msg.TargetName] = new Dictionary<ulong, TaskCompletionSource<ProcessMessage>>();
            Dictionary<ulong, TaskCompletionSource<ProcessMessage>> processtcs = this.ResponseQueue[msg.TargetName];
            processtcs.Add(msg.ID, new TaskCompletionSource<ProcessMessage>());

            string data = msg.Serialize();
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
            catch(Exception e)
            {
                this.CallErrorEvent(e);
            }
        }

        internal TaskCompletionSource<ProcessMessage> GetTCS(string process, ulong id)
        {
            if (this.ResponseQueue.ContainsKey(process))
            {
                Dictionary<ulong, TaskCompletionSource<ProcessMessage>> processtcs = this.ResponseQueue[process];
                if (processtcs.ContainsKey(id))
                    return processtcs[id];
            }

            TaskCompletionSource<ProcessMessage> failtcs = new TaskCompletionSource<ProcessMessage>();
            failtcs.SetException(new Exception($"No TCS for process {process} with message id {id}"));

            return failtcs;
        }

        public RemoteProcess ListenToProcess(string process)
            => new RemoteProcess(this, process);
    }
}
