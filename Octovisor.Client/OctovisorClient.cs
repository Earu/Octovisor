using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Octovisor.Client
{
    public class OctovisorClient
    {
        private const int Port = 11000;

        private readonly ManualResetEvent OnConnectDone;
        private readonly ManualResetEvent OnSendDone;
        private readonly ManualResetEvent OnReceiveDone;

        private Socket Client;

        private string Response = string.Empty;

        public OctovisorClient()
        {
            this.OnConnectDone = new ManualResetEvent(false);
            this.OnSendDone    = new ManualResetEvent(false);
            this.OnReceiveDone = new ManualResetEvent(false);
        }

        public void Run()
        { 
            try
            {
                IPHostEntry hostinfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipadr      = hostinfo.AddressList[0];
                IPEndPoint endpoint  = new IPEndPoint(ipadr, Port);

                this.Client = new Socket(ipadr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                this.Client.BeginConnect(endpoint, new AsyncCallback(ConnectCallback), this.Client);
                OnConnectDone.WaitOne();

                // Debug
                OctovisorMessage msg = new OctovisorMessage
                {
                    Data = "fuck you",
                    OriginName = "Test",
                    TargetName = "HAHA",
                    Port = Port,
                    IPV6 = ipadr.ToString()
                };

                Send(this.Client, msg.Serialize());
                OnSendDone.WaitOne();

                Receive(this.Client);
                OnReceiveDone.WaitOne();

                Console.WriteLine("Response received : {0}", Response);
                Console.Read();

                this.Client.Shutdown(SocketShutdown.Both);
                this.Client.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndConnect(ar);

                Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString());

                OnConnectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void Receive(Socket client)
        {
            try
            {
                StateObject state = new StateObject
                {
                    WorkSocket = client
                };

                client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.WorkSocket;

                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    state.Builder.Append(Encoding.ASCII.GetString(state.Buffer, 0, bytesRead));

                    client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    if (state.Builder.Length > 1)
                        Response = state.Builder.ToString();

                    OnReceiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void Send(Socket client, string data)
        {
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;

                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                OnSendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
