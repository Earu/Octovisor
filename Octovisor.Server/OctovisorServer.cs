using Octovisor.Server.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Octovisor.Server
{
    internal class OctovisorServer
    {
        private Socket Listener;

        private readonly ManualResetEvent ResetEvent;
        private readonly OctovisorLogger Logger;
        private readonly string ServerAddress;
        private readonly int ServerPort;
        private readonly List<Thread> ConnectionThreads;
        private readonly Dictionary<string, StateObject> States;

        internal OctovisorServer(string srvadr,int srvport)
        {
            this.ResetEvent        = new ManualResetEvent(false);
            this.Logger            = new OctovisorLogger();
            this.ServerAddress     = srvadr;
            this.ServerPort        = srvport;
            this.ConnectionThreads = new List<Thread>();
            this.States            = new Dictionary<string, StateObject>();
        }

        internal void Run()
        {
            try
            {
                IPHostEntry hostinfo = Dns.GetHostEntry(this.ServerAddress);
                IPAddress ipadr = hostinfo.AddressList[0];
                IPEndPoint endpoint = new IPEndPoint(ipadr, this.ServerPort);

                this.Listener = new Socket(ipadr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                this.Listener.Bind(endpoint);
                this.Listener.Listen(100);

                this.Logger.Log(ConsoleColor.Magenta, "Server", "Waiting for a connection...");

                while (true)
                {
                    this.ResetEvent.Reset();
                    this.Listener.BeginAccept(new AsyncCallback(this.AcceptCallback), this.Listener);
                    this.ResetEvent.WaitOne();
                }
            }
            catch (Exception e)
            {
                this.Logger.Error($"Something went wrong in the main process\n{e}");
            }

            this.Logger.Pause();
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            this.ResetEvent.Set();

            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            StateObject state = new StateObject(handler);

            // This part is very important it brings the processing into a new thread so 
            // it doesnt block other connections 
            Thread thread = new Thread(() => state.WorkSocket.BeginReceive(state.Buffer, 0, 
                StateObject.BufferSize, SocketFlags.None, new AsyncCallback(this.ReadCallback), state));
            thread.Start();

            this.ConnectionThreads.Add(thread);
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
                state.WorkSocket.BeginReceive(state.Buffer, 0, StateObject.BufferSize, SocketFlags.None, new AsyncCallback(this.ReadCallback), state);
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
                this.Logger.Log(ConsoleColor.Yellow, "Process", $"Registering new remote process | {name} @ {state.WorkSocket.RemoteEndPoint}");
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
                this.Logger.Log(ConsoleColor.Yellow, "Process", $"Ending remote process | {name} @ {state.WorkSocket.RemoteEndPoint}");
            }
        }

        private void DispatchMessage(ProcessMessage msg)
        {
            if (this.States.ContainsKey(msg.TargetName))
            {
                StateObject state = this.States[msg.TargetName];
                this.Send(state.WorkSocket, msg.Serialize());
                this.Logger.Log(ConsoleColor.Green, "Message", $"Forwarded {msg.Data.Length} bytes " +
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