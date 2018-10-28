using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Octovisor.Models;

namespace Octovisor.Server
{
    public class OctovisorServer
    {
        private bool ShouldRun;
        private Socket Listener;
        private Thread Thread;

        private readonly ServerConfig Config;
        private readonly ManualResetEvent ResetEvent;
        private readonly Dictionary<string, StateObject> States;

        /// <summary>
        /// Gets the logger.
        /// </summary>
        /// <value>The logger.</value>
        public OctovisorLogger Logger { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Octovisor.Server.OctovisorServer"/> class.
        /// </summary>
        public OctovisorServer(ServerConfig config)
        {
            if (!config.IsValid())
                throw new Exception("Invalid Octovisor server configuration");

            this.Config                = config;
            this.ShouldRun             = true;
            this.ResetEvent            = new ManualResetEvent(false);
            this.Logger                = new OctovisorLogger();
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
#pragma warning disable RECS0034 
            while (this.Thread.IsAlive) ;
#pragma warning restore RECS0034
            this.Thread = null;
        }

        private void InternalRun()
        {
            try
            {
                IPHostEntry hostinfo = Dns.GetHostEntry(this.Config.ServerAddress);
                IPAddress ipadr = hostinfo.AddressList[0];
                IPEndPoint endpoint = new IPEndPoint(ipadr, this.Config.ServerPort);

                this.Listener = new Socket(SocketType.Stream,ProtocolType.Tcp);
                this.Listener.Bind(endpoint);
                this.Listener.Listen(this.Config.MaximumProcesses);

                this.Logger.Write(ConsoleColor.Magenta, "Server", "Waiting for a connection...");

                while (this.ShouldRun)
                {
                    this.ResetEvent.Reset();
                    this.Listener.BeginAccept(this.AcceptCallback, this.Listener);
                    this.ResetEvent.WaitOne();
                }

                foreach (KeyValuePair<string, StateObject> kv in this.States)
                    this.EndRemoteProcess(kv.Key,this.Config.Token);

                this.Listener.Shutdown(SocketShutdown.Both);
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

            try
            {
                state.WorkSocket.BeginReceive(state.Buffer, 0,
                    StateObject.BufferSize, SocketFlags.None, this.ReadCallback, state);
            }
            catch(Exception e)
            {
                this.Logger.Error(e.ToString());
            }
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

                Message msg = Message.Deserialize(content);
                switch (msg.Identifier)
                {
                    case "INTERNAL_OCTOVISOR_PROCESS_INIT":
                        this.RegisterRemoteProcess(msg.OriginName, state, msg.Data);
                        break;
                    case "INTERNAL_OCTOVISOR_PROCESS_END":
                        this.EndRemoteProcess(msg.OriginName, msg.Data);
                        break;
                    default:
                        this.DispatchMessage(msg);
                        break;
                }

                // Wait again for incoming data
                state.WorkSocket.BeginReceive(state.Buffer, 0, 
                    StateObject.BufferSize, SocketFlags.None, this.ReadCallback, state);
            }
        }

        private void Send(StateObject state, Message msg)
        {
            try
            {
                Socket handler = state.WorkSocket;
                byte[] bytedata = Encoding.UTF8.GetBytes(msg.Serialize());

                handler.BeginSend(bytedata, 0, bytedata.Length, 0, this.SendCallback, handler);
            }
            catch(SocketException)
            {
                this.Logger.Error($"The remote process ({msg.TargetName}) is not available because it was forcibly closed.");
                this.EndRemoteProcess(msg.TargetName,msg.Data);
            }
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

        private void RegisterRemoteProcess(string name,StateObject state, string token)
        {
            if (this.States.ContainsKey(name))
                this.Logger.Warn($"Cannot register remote process with an existing name ({name}). Discarding.");
            else if (this.States.Count >= this.Config.MaximumProcesses)
                this.Logger.Error($"Could not register remote process {name}. Exceeding the maximum amount of remote processes.");
            else
            {
                this.States.Add(name, state);
                this.Logger.Write(ConsoleColor.Yellow, "Process", $"Registering new remote process | {name} @ {state.WorkSocket.RemoteEndPoint}");
            }
        }

        private void EndRemoteProcess(string name, string token)
        {
            if (token != this.Config.Token)
                this.Logger.Warn($"Attempt to end a remote process with invalid token.");
            else if (!this.States.ContainsKey(name))
                this.Logger.Warn($"Attempt to end a non-existing remote process ({name}). Discarding.");
            else
            {
                StateObject state = this.States[name];
                state.WorkSocket.Shutdown(SocketShutdown.Both);
                state.WorkSocket.Close();
                this.States.Remove(name);
                this.Logger.Write(ConsoleColor.Yellow, "Process", $"Ending remote process | {name} @ {state.WorkSocket.RemoteEndPoint}");
            }
        }

        private void DispatchMessage(Message msg)
        {
            if (this.States.ContainsKey(msg.TargetName) && this.States.ContainsKey(msg.OriginName))
            {
                StateObject state = this.States[msg.TargetName];
                this.Send(state,msg);
                this.Logger.Write(ConsoleColor.Green, "Message", $"Forwarded {msg.Data.Length} bytes " +
                    $"| (ID: {msg.Identifier}) {msg.OriginName} -> {msg.TargetName}");
            }
            else
            {
                if (!this.States.ContainsKey(msg.OriginName))
                {
                    this.Logger.Warn($"Unknown remote process {msg.OriginName} tried to forward {msg.Data.Length} bytes to {msg.TargetName}");
                }
                else
                {
                    this.Logger.Warn($"No such remote process ({msg.TargetName}). Sending message back.");
                    StateObject state = this.States[msg.OriginName];
                    this.Send(state, msg);
                }
            }
        }
    }
}