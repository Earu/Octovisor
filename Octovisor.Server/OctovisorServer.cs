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
                IPEndPoint endpoint  = new IPEndPoint(IPAddress.IPv6Any, this.Config.ServerPort);

                this.Listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
                this.Listener.DualMode = true;
                this.Listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, 0);
                this.Listener.Bind(endpoint);
                this.Listener.Listen(this.Config.MaximumProcesses);

                this.Logger.Write(ConsoleColor.Magenta, "Server", 
                    $"Listening for connections at {endpoint}...");

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

        private string HandleReceivedData(StateObject state, int bytesread)
        {
            string content = Encoding.UTF8.GetString(state.Buffer, 0, bytesread);
            string fullsmsg = null;
            foreach (char c in content)
            {
                state.Builder.Append(c);

                string current = state.Builder.ToString();
                int endlen = MessageFinalizer.Length;
                if (current.Length >= endlen && current.Substring(current.Length - endlen, endlen) == MessageFinalizer)
                {
                    fullsmsg = state.Builder.ToString();
                    state.Builder.Clear();
                }
            }

            return fullsmsg;
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
                    string fullsmsg = this.HandleReceivedData(state, bytesread);
                    bool receivemore = false;

                    if (!string.IsNullOrWhiteSpace(fullsmsg))
                    {
                        fullsmsg = fullsmsg.Substring(0, fullsmsg.Length - MessageFinalizer.Length);
                        Message msg = Message.Deserialize(fullsmsg);
                        switch (msg.Identifier)
                        {
                            case "INTERNAL_OCTOVISOR_PROCESS_INIT":
                                receivemore = this.RegisterRemoteProcess(state, msg.OriginName, msg.Data);
                                this.SendbackMessage(state, msg, MessageStatus.DataResponse, receivemore.ToString().ToLower());
                                break;
                            case "INTERNAL_OCTOVISOR_PROCESS_END":
                                receivemore = this.EndRemoteProcess(msg.OriginName, msg.Data);
                                break;
                            default:
                                receivemore = this.DispatchMessage(state, msg);
                                break;
                        }
                    }

                    // Wait again for incoming data
                    if (!state.IsDisposed && receivemore)
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
                handler.Send(bytedata);
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

        private bool RegisterRemoteProcess(StateObject state, string name, string token)
        {
            if (token != this.Config.Token)
            {
                this.Logger.Warn($"Attempt to register a remote process ({name}) with an invalid token.");
                return false;
            }
            else if (this.EndpointLookup.ContainsKey(name))
            {
                this.Logger.Warn($"Cannot register remote process with an existing name ({name}). Discarding.");
                return false;
            }
            else if (this.States.Count >= this.Config.MaximumProcesses)
            {
                this.Logger.Error($"Could not register a remote process ({name}). Exceeding the maximum amount of remote processes.");
                return false;
            }
            else
            {
                EndPoint endpoint = state.WorkSocket.RemoteEndPoint;
                state.Identifier = name;
                this.States.Add(endpoint, state);
                this.EndpointLookup.Add(name, endpoint);
                this.Logger.Write(ConsoleColor.Yellow, "Process", $"Registering new remote process | {name} @ {endpoint}");

                return true;
            }
        }

        private bool EndRemoteProcess(string name, string token)
        {
            if (token != this.Config.Token)
            {
                this.Logger.Warn($"Attempt to end a remote process ({name}) with an invalid token.");
                return false;
            }
            else if (!this.EndpointLookup.ContainsKey(name))
            {
                this.Logger.Warn($"Attempt to end a non-existing remote process ({name}). Discarding.");
                return false;
            }
            else
            {
                EndPoint endpoint = this.EndpointLookup[name];
                StateObject state = this.States[endpoint];
                state.Dispose();
                this.States.Remove(endpoint);
                this.EndpointLookup.Remove(name);

                this.Logger.Write(ConsoleColor.Yellow, "Process", $"Ending remote process | {name} @ {endpoint}");
                return true;
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

            string sufix = $"| (ID: {msg.Identifier}) {msg.OriginName} -> {msg.TargetName}";
            if (msg.Status == MessageStatus.DataRequest)
                this.Logger.Write(ConsoleColor.Green, "Message", $"Requesting data {sufix}");
            else if (msg.Status == MessageStatus.DataResponse)
                this.Logger.Write(ConsoleColor.Green, "Message", $"Forwarded {msg.Length} bytes {sufix}");
        }

        private void SendbackMessage(StateObject state, Message msg,MessageStatus status,string data=null)
        {
            string target  = msg.TargetName;
            msg.TargetName = msg.OriginName;
            msg.OriginName = target;
            msg.Data       = data ?? msg.Data;
            msg.Status     = status;

            this.Send(state, msg);
        }

        private bool DispatchMessage(StateObject state, Message msg)
        {
            if (msg.Status == MessageStatus.MalformedMessageError)
            {
                this.Logger.Warn($"Malformed message received!\n{msg.Data}");
                return false;
            }

            if (this.EndpointLookup.ContainsKey(msg.TargetName) && this.EndpointLookup.ContainsKey(msg.OriginName))
            {
                this.ForwardMessage(msg);
                return true;
            }
            else
            {
                if (!this.EndpointLookup.ContainsKey(msg.OriginName))
                {
                    this.Logger.Warn($"Unknown remote process {msg.OriginName} tried to forward {msg.Length} bytes to {msg.TargetName}");
                    return false;
                }
                else
                {
                    this.Logger.Warn($"{msg.OriginName} tried to forward {msg.Length} bytes to unknown remote process {msg.TargetName}");
                    this.SendbackMessage(state, msg, MessageStatus.ProcessNotFound);
                    return true;
                }
            }
        }
    }
}