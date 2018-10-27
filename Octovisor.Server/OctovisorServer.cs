using Octovisor.Server.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Octovisor.Server
{
    public class OctovisorServer
    {
        private bool ShouldRun;
        private Socket Listener;
        private Thread Thread;

        private readonly int ConnectionQueueLength;
        private readonly ManualResetEvent ResetEvent;
        private readonly string ServerAddress;
        private readonly int ServerPort;
        private readonly Dictionary<string, StateObject> States;

        /// <summary>
        /// Gets the logger.
        /// </summary>
        /// <value>The logger.</value>
        public OctovisorLogger Logger { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Octovisor.Server.OctovisorServer"/> class.
        /// </summary>
        /// <param name="srvadr">The address or domain to listen to</param>
        /// <param name="srvport">The port to use</param>
        /// <param name="cqlen">The maximum amount of connections in the queue</param>
        public OctovisorServer(string srvadr,int srvport,int cqlen=255)
        {
            this.ConnectionQueueLength = cqlen;
            this.ShouldRun             = true;
            this.ResetEvent            = new ManualResetEvent(false);
            this.Logger                = new OctovisorLogger();
            this.ServerAddress         = srvadr;
            this.ServerPort            = srvport;
            this.States                = new Dictionary<string, StateObject>();
        }

        /// <summary>
        /// Run this instance.
        /// </summary>
        public void Run()
        {
            if (this.Thread != null) return;

            this.ShouldRun = true;
            this.Thread = new Thread(this.InternalRun);
            this.Thread.Start();
        }

        /// <summary>
        /// Stop this instance.
        /// </summary>
        public void Stop()
        {
            if (this.Thread == null) return;

            this.ShouldRun = false;
            //Hack to wait that the thread finishes
            while (this.Thread.IsAlive) ;
            this.Thread = null;
        }

        private void InternalRun()
        {
            try
            {
                IPHostEntry hostinfo = Dns.GetHostEntry(this.ServerAddress);
                IPAddress ipadr = hostinfo.AddressList[0];
                IPEndPoint endpoint = new IPEndPoint(ipadr, this.ServerPort);

                this.Listener = new Socket(SocketType.Stream,ProtocolType.Tcp);
                this.Listener.Bind(endpoint);
                this.Listener.Listen(this.ConnectionQueueLength);

                this.Logger.Write(ConsoleColor.Magenta, "Server", "Waiting for a connection...");

                while (this.ShouldRun)
                {
                    this.ResetEvent.Reset();
                    this.Listener.BeginAccept(new AsyncCallback(this.AcceptCallback), this.Listener);
                    this.ResetEvent.WaitOne();
                }

                this.Listener.Close();
            }
            catch (Exception e)
            {
                this.Logger.Error($"Something went wrong in the main process\n{e}");
                this.Logger.Read();
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            this.ResetEvent.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            StateObject state = new StateObject(handler);

            // This part is very important it brings the processing into a new thread so 
            // it doesnt block other connections 
            Thread thread = new Thread(() =>
            {
                try
                {
                    state.WorkSocket.BeginReceive(state.Buffer, 0,
                        StateObject.BufferSize, SocketFlags.None, new AsyncCallback(this.ReadCallback), state);
                }
                catch(Exception e)
                {
                    this.Logger.Error(e.ToString());
                }
            });
            thread.Start();
        }

        private void ReadCallback(IAsyncResult ar)
        {
            string content = string.Empty;

            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.WorkSocket;

            int bytesread = handler.EndReceive(ar);

            if (bytesread > 0)
            {
                state.Builder.Clear();
                state.Builder.Append(Encoding.UTF8.GetString(state.Buffer, 0, bytesread));
                content = state.Builder.ToString();

                ProcessMessage msg = ProcessMessage.Deserialize(content);
                switch (msg.MessageIdentifier)
                {
                    case "INTERNAL_OCTOVISOR_PROCESS_INIT":
                        this.RegisterRemoteProcess(msg.OriginName, state);
                        break;
                    case "INTERNAL_OCTOVISOR_PROCESS_END":
                        this.EndRemoteProcess(msg.OriginName);
                        break;
                    default:
                        this.DispatchMessage(msg);
                        break;
                }

                // Wait again for incoming data
                state.WorkSocket.BeginReceive(state.Buffer, 0, 
                    StateObject.BufferSize, SocketFlags.None, new AsyncCallback(this.ReadCallback), state);
            }
        }

        private void Send(Socket handler, string data)
        {
            byte[] bytedata = Encoding.UTF8.GetBytes(data);

            handler.BeginSend(bytedata, 0, bytedata.Length, 0, new AsyncCallback(this.SendCallback), handler);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                handler.EndSend(ar);
            }
            catch (Exception e)
            {
                this.Logger.Error($"Something went wrong when sending data\n{e}");
            }
        }

        private void RegisterRemoteProcess(string name,StateObject state)
        {
            if (this.States.ContainsKey(name))
                this.Logger.Warn($"Cannot register remote process with an existing name ({name}). Discarding.");
            else
            {
                this.States.Add(name, state);
                this.Logger.Write(ConsoleColor.Yellow, "Process", $"Registering new remote process | {name} @ {state.WorkSocket.RemoteEndPoint}");
            }
        }

        private void EndRemoteProcess(string name)
        {
            if (!this.States.ContainsKey(name))
                this.Logger.Warn($"Attempt to end a non-existing remote process ({name}). Discarding.");
            else
            {
                StateObject state = this.States[name];
                this.States.Remove(name);
                this.Logger.Write(ConsoleColor.Yellow, "Process", $"Ending remote process | {name} @ {state.WorkSocket.RemoteEndPoint}");
            }
        }

        private void DispatchMessage(ProcessMessage msg)
        {
            if (this.States.ContainsKey(msg.TargetName))
            {
                StateObject state = this.States[msg.TargetName];
                this.Send(state.WorkSocket, msg.Serialize());
                this.Logger.Write(ConsoleColor.Green, "Message", $"Forwarded {msg.Data.Length} bytes " +
                    $"| (ID: {msg.MessageIdentifier}) {msg.OriginName} -> {msg.TargetName}");
            }
            else
            {
                this.Logger.Warn($"No such remote process ({msg.TargetName}). Sending message back.");
                StateObject state = this.States[msg.OriginName];
                this.Send(state.WorkSocket, msg.Serialize());
            }
        }
    }
}