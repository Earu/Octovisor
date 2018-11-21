using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octovisor.Models;
using System.IO;

namespace Octovisor.Server
{
    public class OctovisorServer
    {
        private static readonly string MessageFinalizer = "__END__";

        private bool ShouldRun;
        private TcpListener Listener;
        private Thread Thread;

        private readonly ServerConfig Config;
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
            this.Logger         = new OctovisorLogger();
            this.States         = new Dictionary<EndPoint, StateObject>();
            this.EndpointLookup = new Dictionary<string, EndPoint>();
        }

        /// <summary>
        /// Run this instance.
        /// </summary>
        public void Run()
        {
            if(this.Thread != null)
            {
                if(this.Thread.IsAlive)
                    this.Stop();
                else
                    return;
            }

            this.ShouldRun = true;
            this.Thread = new Thread(this.InternalRun);
            this.Thread.Start();
        }

        /// <summary>
        /// Stop this instance.
        /// </summary>
        public void Stop()
        {
            if(this.Thread == null) return;

            this.ShouldRun = false;
            while(this.Thread.IsAlive);
            this.Thread = null;
        }

        private void InternalRun()
        {
            try
            {
                IPEndPoint endpoint  = new IPEndPoint(IPAddress.IPv6Any, this.Config.ServerPort);
                TcpListener listener = new TcpListener(endpoint);
                // To accept IPv4 and IPv6
                listener.Server.DualMode = true;
                listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, 0);
                listener.Start(this.Config.MaximumProcesses);

                this.Listener = listener;

                this.Logger.Write(ConsoleColor.Magenta, "Server", 
                    $"Listening for connections at {endpoint}...");

                // No await = no block
                while (this.ShouldRun)
                    #pragma warning disable CS4014
                    this.ProcessConnection();
                    #pragma warning restore CS4014

                foreach (KeyValuePair<string, EndPoint> kv in this.EndpointLookup)
                    this.EndRemoteProcess(kv.Key, this.Config.Token);

                this.Listener.Stop();
            }
            catch (Exception e)
            {
                this.Logger.Error($"Something went wrong in the main process\n{e}");
                this.Logger.Read();
            }
        }

        private async Task ProcessConnection()
        {
            TcpClient client = await this.Listener.AcceptTcpClientAsync();
            StateObject state = new StateObject(client);

            try
            {
                await this.ListenRemoteProcess(state);
            }
            catch(SocketException e)
            {
                this.ProcessException(state, e);
            }
            catch(Exception e)
            {
                this.Logger.Error(e.ToString());
            }
        }

        private void ProcessException(StateObject state,Exception e)
        {
            SocketException se = null;
            if(e is SocketException)
                se = (SocketException)e;
            else if(e.InnerException is SocketException) 
                se = (SocketException)e.InnerException;

            //10054
            if (se != null && se.SocketErrorCode == SocketError.ConnectionReset)
            {
                this.Logger.Warn($"Process {state.Identifier} was forcibly closed");
                this.EndRemoteProcess(state.RemoteEndPoint);
            }
            else
                this.Logger.Error(e.ToString());
        }

        private List<string> HandleReceivedData(StateObject state, int bytesread)
        {
            string content = Encoding.UTF8.GetString(state.Buffer, 0, bytesread);
            List<string> msgdata = new List<string>();
            foreach (char c in content)
            {
                state.Builder.Append(c);

                string current = state.Builder.ToString();
                int endlen = MessageFinalizer.Length;
                if (current.Length >= endlen && current.Substring(current.Length - endlen, endlen) == MessageFinalizer)
                {
                    msgdata.Add(state.Builder.ToString());
                    state.Builder.Clear();
                }
            }

            return msgdata;
        }

        private async Task ListenRemoteProcess(StateObject state)
        {
            try
            {
                TcpClient client = state.Client;
                NetworkStream stream = client.GetStream();
                int bytesread = await stream.ReadAsync(state.Buffer);

                if (bytesread > 0)
                {
                    List<string> msgdata = this.HandleReceivedData(state, bytesread);
                    bool receivemore = false;

                    foreach(string data in msgdata)
                    {
                        string smsg = data.Substring(0, data.Length - MessageFinalizer.Length);
                        Message msg = Message.Deserialize(smsg);
                        switch (msg.Identifier)
                        {
                            case "INTERNAL_OCTOVISOR_PROCESS_INIT":
                                receivemore = this.RegisterRemoteProcess(state, msg.OriginName, msg.Data);
                                await this.SendbackMessage(state, msg, MessageStatus.DataResponse, receivemore.ToString().ToLower());
                                break;
                            case "INTERNAL_OCTOVISOR_PROCESS_END":
                                receivemore = this.EndRemoteProcess(msg.OriginName, msg.Data);
                                break;
                            default:
                                receivemore = await this.DispatchMessage(state, msg);
                                break;
                        }
                    }

                    // Wait again for incoming data
                    if (!state.IsDisposed && receivemore)
                        #pragma warning disable CS4014
                        this.ListenRemoteProcess(state);
                        #pragma warning restore CS4014
                }
            }
            catch (Exception e)
            {
                this.ProcessException(state, e);
            }
        }

        private async Task Send(StateObject state, Message msg)
        {
            try
            {
                NetworkStream stream = state.Client.GetStream();
                byte[] bytedata = Encoding.UTF8.GetBytes(msg.Serialize() + MessageFinalizer);
                await stream.WriteAsync(bytedata);
                await stream.FlushAsync();
            }
            catch (Exception e)
            {
                this.ProcessException(state, e);
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
                EndPoint endpoint = state.RemoteEndPoint;
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

        private async Task ForwardMessage(Message msg)
        {
            EndPoint endpoint = this.EndpointLookup[msg.TargetName];
            StateObject state = this.States[endpoint];
            await this.Send(state, msg);

            string sufix = $"| (ID: {msg.Identifier}) {msg.OriginName} -> {msg.TargetName}";
            if (msg.Status == MessageStatus.DataRequest)
                this.Logger.Write(ConsoleColor.Green, "Message", $"Requesting data {sufix}");
            else if (msg.Status == MessageStatus.DataResponse)
                this.Logger.Write(ConsoleColor.Green, "Message", $"Forwarded {msg.Length} bytes {sufix}");
        }

        private async Task SendbackMessage(StateObject state, Message msg,MessageStatus status,string data=null)
        {
            string target  = msg.TargetName;
            msg.TargetName = msg.OriginName;
            msg.OriginName = target;
            msg.Data       = data ?? msg.Data;
            msg.Status     = status;

            await this.Send(state, msg);
        }

        private async Task<bool> DispatchMessage(StateObject state, Message msg)
        {
            if (msg.Status == MessageStatus.MalformedMessageError)
            {
                this.Logger.Warn($"Malformed message received!\n{msg.Data}");
                return false;
            }

            if (this.EndpointLookup.ContainsKey(msg.TargetName) && this.EndpointLookup.ContainsKey(msg.OriginName))
            {
                await this.ForwardMessage(msg);
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
                    await this.SendbackMessage(state, msg, MessageStatus.ProcessNotFound);
                    return true;
                }
            }
        }
    }
}