using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Octovisor.Server
{
    internal class OctovisorServer
    {
        private readonly ManualResetEvent ResetEvent;
        private readonly OctovisorLogger Logger;
        private Socket Listener;

        internal OctovisorServer()
        {
            this.ResetEvent = new ManualResetEvent(false);
            this.Logger     = new OctovisorLogger();
        }

        internal void Run()
        {
            try
            {
                IPHostEntry hostinfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipadr = hostinfo.AddressList[0];
                IPEndPoint endpoint = new IPEndPoint(ipadr, 11000);

                this.Listener = new Socket(ipadr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                this.Listener.Bind(endpoint);
                this.Listener.Listen(100);

                while (true)
                {
                    this.ResetEvent.Reset();

                    this.Logger.Log(ConsoleColor.Magenta, "Server", "Waiting for a connection...");
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

            StateObject state = new StateObject
            {
                WorkSocket = handler
            };
            handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(this.ReadCallback), state);
        }

        private void ReadCallback(IAsyncResult ar)
        {
            string content = string.Empty;

            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.WorkSocket;

            int bytesread = handler.EndReceive(ar);

            if (bytesread > 0)
            {
                state.Builder.Append(Encoding.ASCII.GetString(state.Buffer, 0, bytesread));
                content = state.Builder.ToString();

                OctovisorMessage msg = OctovisorMessage.Deserialize(content);

                this.Logger.Log(ConsoleColor.Green,"Message", $"{msg.OriginName} -> {msg.TargetName} | Forwarding {content.Length} bytes");
                this.Send(handler, content);
            }
        }

        private void Send(Socket handler, string data)
        {
            byte[] bytedata = Encoding.ASCII.GetBytes(data);

            handler.BeginSend(bytedata, 0, bytedata.Length, 0, new AsyncCallback(this.SendCallback), handler);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                handler.EndSend(ar);
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception e)
            {
                this.Logger.Error($"Something went wrong when closing a connection\n{e}");
            }
        }
    }
}