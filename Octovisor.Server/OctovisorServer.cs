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
        private static readonly string MessageFinalizer = "__END__";

        private bool ShouldRun;
        private Socket Listener;
        private Thread Thread;

        private readonly ServerConfig Config;
        private readonly ManualResetEvent ResetEvent;
        private readonly Dictionary<EndPoint, StateObject> States;
        private readonly Dictionary<string, EndPoint> EndpointLookup;

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

            this.Config         = config;
            this.ShouldRun      = true;
            this.ResetEvent     = new ManualResetEvent(false);
            this.Logger         = new OctovisorLogger();
            this.States         = new Dictionary<EndPoint, StateObject>();
            this.EndpointLookup = new Dictionary<string, EndPoint>();
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
                IPAddress ipadr      = hostinfo.AddressList[0];
                IPEndPoint endpoint  = new IPEndPoint(ipadr, this.Config.ServerPort);

                this.Listener = new Socket(SocketType.Stream,ProtocolType.Tcp);
                this.Listener.Bind(endpoint);
                this.Listener.Listen(this.Config.MaximumProcesses);

                this.Logger.Write(ConsoleColor.Magenta, "Server", 
                    $"Listening for connections at {this.Config.ServerAddress}:{this.Config.ServerPort}...");

                while (this.ShouldRun)
                {
                    this.ResetEvent.Reset();
                    this.Listener.BeginAccept(this.AcceptCallback, this.Listener);
                    this.ResetEvent.WaitOne();
                }

                foreach (KeyValuePair<string, EndPoint> kv in this.EndpointLookup)
                    this.EndRemoteProcess(kv.Key, this.Config.Token);

                this.Listener.Shutdown(SocketShutdown.Both);
                this.Listener.Close();
            }
            catch (Exception e)
            {
                this.Logger.Error($"Something went wrong in the main process\n{e}");
                this.Logger.Read();
            }
        }

        private void ProcessSocketException(StateObject state,SocketException e)
        {
            //10054
            if (e.SocketErrorCode == SocketError.ConnectionReset)
            {
                this.Logger.Warn($"Process {state.Identifier} was forcibly closed");
                this.EndRemoteProcess(state.WorkSocket.RemoteEndPoint);
            }
            else
            {
                this.Logger.Error(e.ToString());
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            this.ResetEvent.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler  = listener.EndAccept(ar);

            StateObject state = new StateObject(handler);

            try
            {
                state.WorkSocket.BeginReceive(state.Buffer, 0,
                    StateObject.BufferSize, SocketFlags.None, this.ReadCallback, state);
            }
            catch(SocketException e)
            {
                this.ProcessSocketException(state, e);
            }
            catch(Exception e)
            {
                this.Logger.Error(e.ToString());
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.WorkSocket;

            try
            {
                int bytesread = handler.EndReceive(ar);

                if (bytesread > 0)
                {
                    string content = Encoding.UTF8.GetString(state.Buffer, 0, bytesread);
                    string fullsmsg = null;
                    foreach(char c in content)
                    {
                        state.Builder.Append(c);

                        string current = state.Builder.ToString();
                        int endlen = MessageFinalizer.Length;
                        if (current.Length >= endlen && current.Substring(current.Length - endlen,endlen) == MessageFinalizer)
                        {
                            fullsmsg = state.Builder.ToString();
                            state.Builder.Clear();
                        }
                    }

                    if(!string.IsNullOrWhiteSpace(fullsmsg))
                    {
                        fullsmsg = fullsmsg.Substring(0, fullsmsg.Length - MessageFinalizer.Length);
                        Message msg = Message.Deserialize(fullsmsg);
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
                    }

                    // Wait again for incoming data
                    if (!state.IsDisposed)
                        state.WorkSocket.BeginReceive(state.Buffer, 0,
                            StateObject.BufferSize, SocketFlags.None, this.ReadCallback, state);
                }
            }
            catch (SocketException e)
            {
                this.ProcessSocketException(state, e);
            }
            catch (Exception e)
            {
                this.Logger.Error(e.ToString());
            }
        }

        private void Send(StateObject state, Message msg)
        {
            try
            {
                Socket handler = state.WorkSocket;
                byte[] bytedata = Encoding.UTF8.GetBytes(msg.Serialize() + MessageFinalizer);

                handler.BeginSend(bytedata, 0, bytedata.Length, 0, this.SendCallback, handler);
            }
            catch (SocketException e)
            {
                this.ProcessSocketException(state, e);
            }
            catch (Exception e)
            {
                this.Logger.Error(e.ToString());
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
            if (this.EndpointLookup.ContainsKey(name))
                this.Logger.Warn($"Cannot register remote process with an existing name ({name}). Discarding.");
            else if (this.States.Count >= this.Config.MaximumProcesses)
                this.Logger.Error($"Could not register a remote process ({name}). Exceeding the maximum amount of remote processes.");
            else
            {
                EndPoint endpoint = state.WorkSocket.RemoteEndPoint;
                state.Identifier = name;
                this.States.Add(endpoint, state);
                this.EndpointLookup.Add(name, endpoint);
                this.Logger.Write(ConsoleColor.Yellow, "Process", $"Registering new remote process | {name} @ {endpoint}");
            }
        }

        private void EndRemoteProcess(string name, string token)
        {
            if (token != this.Config.Token)
                this.Logger.Warn($"Attempt to end a remote process ({name}) with an invalid token.");
            else if (!this.EndpointLookup.ContainsKey(name))
                this.Logger.Warn($"Attempt to end a non-existing remote process ({name}). Discarding.");
            else
            {
                EndPoint endpoint = this.EndpointLookup[name];
                StateObject state = this.States[endpoint];
                state.Dispose();
                this.States.Remove(endpoint);
                this.EndpointLookup.Remove(name);

                this.Logger.Write(ConsoleColor.Yellow, "Process", $"Ending remote process | {name} @ {endpoint}");
            }
        }

        // When client closes brutally
        private void EndRemoteProcess(EndPoint endpoint)
        {
            if(this.States.ContainsKey(endpoint))
            {
                StateObject state = this.States[endpoint];
                state.Dispose();
                this.States.Remove(endpoint);
                this.EndpointLookup.Remove(state.Identifier);

                this.Logger.Write(ConsoleColor.Yellow, "Process", $"Ending remote process | {state.Identifier} @ {endpoint}");
            }
        }

        private void ForwardMessage(Message msg)
        {
            EndPoint endpoint = this.EndpointLookup[msg.TargetName];
            StateObject state = this.States[endpoint];

            this.Send(state, msg);
            this.Logger.Write(ConsoleColor.Green, "Message", $"Forwarded {msg.Data.Length} bytes " +
                $"| (ID: {msg.Identifier}) {msg.OriginName} -> {msg.TargetName}");
        }

        private void SendbackMessage(Message msg,MessageStatus status,string data=null)
        {
            EndPoint endpoint = this.EndpointLookup[msg.OriginName];
            StateObject state = this.States[endpoint];

            msg.Data = data ?? msg.Data;
            msg.Status = status;
            this.Send(state, msg);
        }

        private void DispatchMessage(Message msg)
        {
            if (msg.Status == MessageStatus.MalformedMessageError)
            {
                this.Logger.Warn($"Malformed message received!\n{msg.Data}");
                return;
            }

            if (this.EndpointLookup.ContainsKey(msg.TargetName) && this.EndpointLookup.ContainsKey(msg.OriginName))
                this.ForwardMessage(msg);
            else
            {
                if (!this.EndpointLookup.ContainsKey(msg.OriginName))
                    this.Logger.Warn($"Unknown remote process {msg.OriginName} tried to forward {msg.Length} bytes to {msg.TargetName}");
                else
                {
                    this.Logger.Warn($"{msg.OriginName} tried to forward {msg.Length} bytes to unknown remote process {msg.TargetName}");
                    this.SendbackMessage(msg, MessageStatus.ProcessNotFound);
                }
            }
        }
    }
}