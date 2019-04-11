using Octovisor.Messages;
using Octovisor.Server.Properties;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Octovisor.Server
{
    internal class Server
    {
        private bool _ShouldRun;
        private TcpListener _Listener;
        private Task _InternalTask;

        private readonly string _MessageFinalizer;
        private readonly Dictionary<EndPoint, ClientState> _States;
        private readonly Dictionary<string, EndPoint> _EndpointLookup;
        private readonly Logger _Logger;
        private readonly MessageFactory _MessageFactory;

        internal Server()
        {
            Console.Clear();
            Console.Title = Resources.Title;

            this._ShouldRun = true;
            this._MessageFinalizer = Config.Instance.MessageFinalizer;
            this._Logger = new Logger();
            this._MessageFactory = new MessageFactory();
            this._States = new Dictionary<EndPoint, ClientState>();
            this._EndpointLookup = new Dictionary<string, EndPoint>();
        }

        private void DisplayAsciiArt()
        {
            ConsoleColor[] colors =
            {
                ConsoleColor.Blue, ConsoleColor.Cyan, ConsoleColor.Green,
                ConsoleColor.Yellow, ConsoleColor.Red, ConsoleColor.Magenta,
            };
            string ascii = Resources.Ascii;
            string[] lines = ascii.Split('\n');
            Random rand = new Random();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                ConsoleColor col = colors[i];
                Console.ForegroundColor = col;
                Console.WriteLine($"\t{line}");
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n" + new string('-', 70));
        }

        internal Task Run()
        {
            if(this._InternalTask != null)
                this.Stop();

            this._ShouldRun = true;
            this._InternalTask = this.InternalRun();

            return this._InternalTask;
        }

        private void Stop()
        {
            if(this._InternalTask == null) return;

            this._ShouldRun = false;
            this._InternalTask.Wait();
        }

        private async Task InternalRun()
        {
            this.DisplayAsciiArt();
            try
            {
                IPEndPoint endpoint  = new IPEndPoint(IPAddress.IPv6Any, Config.Instance.Port);
                TcpListener listener = new TcpListener(endpoint);
                // To accept IPv4 and IPv6
                listener.Server.DualMode = true;
                listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, 0);
                listener.Start(Config.Instance.MaxProcesses);

                this._Listener = listener;
                this._Logger.Nice("Server", ConsoleColor.Magenta, $"Running on {endpoint}...");

                while (this._ShouldRun)
                    await this.ProcessConnection();

                foreach (KeyValuePair<string, EndPoint> kv in this._EndpointLookup)
                    this.EndRemoteProcess(kv.Key, Config.Instance.Token);

                this._Listener.Stop();
            }
            catch (Exception e)
            {
                this._Logger.Error($"Something went wrong in the main process\n{e}");
                Console.ReadLine();
            }
        }

        private async Task ProcessConnection()
        {
            TcpClient client = null;
            try
            {
                client = await this._Listener.AcceptTcpClientAsync();
            }
            catch (Exception e)
            {
                this.OnException(e);
            }

            if (client == null) return;
            ClientState state = new ClientState(client, this._MessageFinalizer);

            #pragma warning disable CS4014
            this.ListenRemoteProcess(state);
            #pragma warning restore CS4014
        }

        private void HandleException(Exception e, Action onconnectionreset)
        {
            SocketException se = null;
            if (e is SocketException)
                se = (SocketException)e;
            else if (e.InnerException is SocketException)
                se = (SocketException)e.InnerException;

            //10054
            if (se != null && se.SocketErrorCode == SocketError.ConnectionReset)
            {
                onconnectionreset();
            }
            else
                this._Logger.Error(e.ToString());
        }

        private void OnClientStateException(ClientState state, Exception e)
        {
            this.HandleException(e, () =>
            {
                this._Logger.Nice("Process", ConsoleColor.Red, $"{state.Identifier} was forcibly closed");
                this.EndRemoteProcess(state.RemoteEndPoint);
            });
        }

        private void OnException(Exception e)
        {
            this.HandleException(e, () =>
            {
                this._Logger.Nice("Process", ConsoleColor.Red, "A remote process was forcibly closed when connecting");
            });
        }

        private async Task ListenRemoteProcess(ClientState state)
        {
            try
            {
                TcpClient client = state.Client;
                NetworkStream stream = client.GetStream();
                int bytesread = await stream.ReadAsync(state.Buffer);
                if (bytesread <= 0) return;

                string data = Encoding.UTF8.GetString(state.Buffer, 0, bytesread);
                List<Message> msgs = state.Reader.Read(data);
                state.ClearBuffer();
                foreach(Message msg in msgs)
                {
                    switch (msg.Identifier)
                    {
                        case "INTERNAL_OCTOVISOR_PROCESS_INIT":
                            this.RegisterRemoteProcess(state, msg.OriginName, msg.Data);
                            await this.AnswerMessage(state, msg, state.IsRegistered ? "true" : "false", MessageStatus.Success);
                            break;
                        case "INTERNAL_OCTOVISOR_PROCESS_END":
                            this.EndRemoteProcess(msg.OriginName, msg.Data);
                            break;
                        default:
                            await this.DispatchMessage(state, msg);
                            break;
                    }
                }

                // Maximum register payload size is 395 bytes, the client is sending garbage.
                if (!state.IsRegistered && state.Reader.Size >= 500)
                    state.Reader.Clear();

                // Wait again for incoming data
                if (!state.IsDisposed && state.IsRegistered)
                    #pragma warning disable CS4014
                    this.ListenRemoteProcess(state);
                    #pragma warning restore CS4014
            }
            catch (Exception e)
            {
                this.OnClientStateException(state, e);
            }
        }

        private async Task Send(ClientState state, Message msg)
        {
            try
            {
                NetworkStream stream = state.Client.GetStream();
                byte[] bytedata = Encoding.UTF8.GetBytes($"{msg.Serialize()}{this._MessageFinalizer}");
                await stream.WriteAsync(bytedata);
            }
            catch (Exception e)
            {
                this.OnClientStateException(state, e);
            }
        }

        private void RegisterRemoteProcess(ClientState state, string name, string token)
        {
            if (token != Config.Instance.Token)
            {
                this._Logger.Warning($"Attempt to register a remote process ({name}) with an invalid token.");
            }
            else if (this._States.Count >= Config.Instance.MaxProcesses)
            {
                this._Logger.Warning($"Could not register a remote process ({name}). Exceeding the maximum amount of remote processes.");
            }
            else
            {
                EndPoint endpoint;

                if (this._EndpointLookup.ContainsKey(name))
                {
                    this._Logger.Nice("Process", ConsoleColor.Yellow, $"Overriding a remote process ({name})");
                    endpoint = this._EndpointLookup[name];
                    this.EndRemoteProcess(endpoint);
                }

                endpoint = state.RemoteEndPoint;
                state.Identifier = name;
                this._States.Add(endpoint, state);
                this._EndpointLookup.Add(name, endpoint);
                state.Register();
                this._Logger.Nice("Process", ConsoleColor.Magenta, $"Registering new remote process | {name} @ {endpoint}");
            }
        }

        private void EndRemoteProcess(string name, string token)
        {
            if (token != Config.Instance.Token)
            {
                this._Logger.Warning($"Attempt to end a remote process ({name}) with an invalid token.");
            }
            else if (!this._EndpointLookup.ContainsKey(name))
            {
                this._Logger.Warning($"Attempt to end a non-existing remote process ({name}). Discarding.");
            }
            else
            {
                EndPoint endpoint = this._EndpointLookup[name];
                ClientState state = this._States[endpoint];
                state.Dispose();
                this._States.Remove(endpoint);
                this._EndpointLookup.Remove(name);

                this._Logger.Nice("Process", ConsoleColor.Magenta, $"Ending remote process | {name} @ {endpoint}");
            }
        }

        // When client closes brutally
        private void EndRemoteProcess(EndPoint endpoint)
        {
            if(this._States.ContainsKey(endpoint))
            {
                ClientState state = this._States[endpoint];
                state.Dispose();
                this._States.Remove(endpoint);
                this._EndpointLookup.Remove(state.Identifier);

                this._Logger.Nice("Process", ConsoleColor.Magenta, $"Ending remote process | {state.Identifier} @ {endpoint}");
            }
        }

        private async Task ForwardMessage(Message msg)
        {
            EndPoint endpoint = this._EndpointLookup[msg.TargetName];
            ClientState state = this._States[endpoint];
            msg.Status = MessageStatus.Success;
            await this.Send(state, msg);

            string tail;
            switch(msg.Type)
            {
                case MessageType.Request:
                    tail = $"| (ID: {msg.Identifier}) {msg.OriginName} -> {msg.TargetName}";
                    this._Logger.Nice("Message", ConsoleColor.Gray, $"Forwarded request {tail}");
                    break;
                case MessageType.Response:
                    tail = $"| (ID: {msg.Identifier}) {msg.TargetName} <- {msg.OriginName}";
                    this._Logger.Nice("Message", ConsoleColor.Gray, $"Forwarded response {msg.Length} bytes {tail}");
                    break;
                case MessageType.Unknown:
                    tail = $"| (ID: {msg.Identifier}) {msg.OriginName} ?? {msg.TargetName}";
                    this._Logger.Nice("Message", ConsoleColor.Yellow, $"Forwarded unknown message type {msg.Length} bytes {tail}");
                    break;
            }
        }

        private async Task AnswerMessage(ClientState state, Message msg, string data = null, MessageStatus status = MessageStatus.Success) 
        {
            Message replymsg = this._MessageFactory.CreateMessageResponse(msg, data, status);
            await this.Send(state, msg);
        }

        private async Task DispatchMessage(ClientState state, Message msg)
        {
            if (msg.IsMalformed)
            {
                this._Logger.Warning($"Malformed message received!\n{msg.Data}");
                return;
            }

            if (this._EndpointLookup.ContainsKey(msg.TargetName) && this._EndpointLookup.ContainsKey(msg.OriginName))
            {
                await this.ForwardMessage(msg);
                return;
            }
            else
            {
                if (!this._EndpointLookup.ContainsKey(msg.OriginName))
                {
                    this._Logger.Warning($"Unknown remote process {msg.OriginName} tried to forward {msg.Length} bytes to {msg.TargetName}");
                }
                else
                {
                    this._Logger.Warning($"{msg.OriginName} tried to forward {msg.Length} bytes to unknown remote process {msg.TargetName}");
                    await this.AnswerMessage(state, msg, null, MessageStatus.ProcessNotFound);
                }
            }
        }
    }
}