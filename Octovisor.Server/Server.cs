using Newtonsoft.Json;
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
        private bool ShouldRun;
        private TcpListener Listener;
        private Task InternalTask;

        private readonly string MessageFinalizer;
        private readonly Dictionary<EndPoint, ClientState> States;
        private readonly Dictionary<string, EndPoint> EndpointLookup;
        private readonly Logger Logger;
        private readonly MessageFactory MessageFactory;

        internal Server()
        {
            Console.Clear();
            Console.Title = Resources.Title;

            this.ShouldRun = true;
            this.MessageFinalizer = Config.Instance.MessageFinalizer;
            this.Logger = new Logger();
            this.MessageFactory = new MessageFactory();
            this.States = new Dictionary<EndPoint, ClientState>();
            this.EndpointLookup = new Dictionary<string, EndPoint>();
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

        internal Task RunAsync()
        {
            if(this.InternalTask != null)
                this.Stop();

            this.ShouldRun = true;
            this.InternalTask = this.InternalRunAsync();

            return this.InternalTask;
        }

        private void Stop()
        {
            if(this.InternalTask == null) return;

            this.ShouldRun = false;
            this.InternalTask.Wait();
        }

        private async Task InternalRunAsync()
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

                this.Listener = listener;
                this.Logger.Nice("Server", ConsoleColor.Magenta, $"Running on {endpoint}...");

                while (this.ShouldRun)
                    await this.ListenConnectionAsync();

                foreach (KeyValuePair<string, EndPoint> kv in this.EndpointLookup)
                    await this.EndProcessAsync(kv.Key, Config.Instance.Token);

                this.Listener.Stop();
            }
            catch (Exception e)
            {
                this.Logger.Error($"Something went wrong in the main process\n{e}");
                Console.ReadLine();
            }
        }

        private async Task ListenConnectionAsync()
        {
            TcpClient client = null;
            try
            {
                client = await this.Listener.AcceptTcpClientAsync();
            }
            catch (Exception e)
            {
                this.OnException(e);
            }

            if (client == null) return;
            ClientState state = new ClientState(client, this.MessageFinalizer);

            #pragma warning disable CS4014
            this.ListenAsync(state);
            #pragma warning restore CS4014
        }

        private void HandleException(Exception e, Action onConnectionReset)
        {
            SocketException se = null;
            if (e is SocketException)
                se = (SocketException)e;
            else if (e.InnerException is SocketException)
                se = (SocketException)e.InnerException;

            //10054
            if (se != null && se.SocketErrorCode == SocketError.ConnectionReset)
            {
                onConnectionReset();
            }
            else
                this.Logger.Error(e.ToString());
        }

        private void OnClientStateException(ClientState state, Exception e)
        {
            this.HandleException(e, async () =>
            {
                this.Logger.Nice("Process", ConsoleColor.Red, $"{state.Name} was forcibly closed");
                this.EndProcess(state.RemoteEndPoint);

                ProcessUpdateData enddata = new ProcessUpdateData(true, state.Name);
                await this.BroadcastMessageAsync(MessageConstants.END_IDENTIFIER, enddata.Serialize());
            });
        }

        private void OnException(Exception e)
        {
            this.HandleException(e, () =>
            {
                this.Logger.Nice("Process", ConsoleColor.Red, "A remote process was forcibly closed when connecting");
            });
        }

        private string RequestProcessesData(string origin)
        {
            List<RemoteProcessData> res = new List<RemoteProcessData>();
            foreach (KeyValuePair<EndPoint, ClientState> state in this.States)
            {
                string name = state.Value.Name;
                if (!name.Equals(origin))
                    res.Add(new RemoteProcessData(state.Value.Name));
            }

            return JsonConvert.SerializeObject(res);
        }

        private async Task HandleReceivedMessageAsync(ClientState state, Message msg)
        {
            switch (msg.Identifier)
            {
                case MessageConstants.REGISTER_IDENTIFIER:
                    await this.RegisterProcessAsync(state, msg.OriginName, msg.Data);
                    break;
                case MessageConstants.END_IDENTIFIER:
                    await this.EndProcessAsync(msg.OriginName, msg.Data);
                    break;
                case MessageConstants.REQUEST_PROCESSES_INFO_IDENTIFIER:
                    string processesData = this.RequestProcessesData(msg.OriginName);
                    await this.AnswerMessageAsync(state, msg, processesData);
                    break;
                default:
                    await this.DispatchMessageAsync(state, msg);
                    break;
            }
        }

        private async Task ListenAsync(ClientState state)
        {
            try
            {
                TcpClient client = state.Client;
                NetworkStream stream = client.GetStream();
                int bytesRead = await stream.ReadAsync(state.Buffer);
                if (bytesRead <= 0) return;

                string data = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);
                List<Message> msgs = state.Reader.Read(data);
                state.ClearBuffer();
                foreach (Message msg in msgs)
                    await this.HandleReceivedMessageAsync(state, msg);

                // Maximum register payload size is 395 bytes, the client is sending garbage.
                if (!state.IsRegistered && state.Reader.Size >= 500)
                    state.Reader.Clear();

                // Wait again for incoming data
                if (!state.IsDisposed && state.IsRegistered)
                    #pragma warning disable CS4014
                    this.ListenAsync(state);
                    #pragma warning restore CS4014
            }
            catch (Exception e)
            {
                this.OnClientStateException(state, e);
            }
        }

        private async Task SendAsync(ClientState state, Message msg)
        {
            try
            {
                NetworkStream stream = state.Client.GetStream();
                byte[] bytes = Encoding.UTF8.GetBytes($"{msg.Serialize()}{this.MessageFinalizer}");
                await stream.WriteAsync(bytes);
            }
            catch (Exception e)
            {
                this.OnClientStateException(state, e);
            }
        }

        private async Task RegisterProcessAsync(ClientState state, string name, string token)
        {
            ProcessUpdateData data;
            if (token != Config.Instance.Token)
            {
                this.Logger.Warning($"Attempt to register a remote process ({name}) with an invalid token.");
                data = new ProcessUpdateData(false, name);
            }
            else if (this.States.Count >= Config.Instance.MaxProcesses)
            {
                this.Logger.Warning($"Could not register a remote process ({name}). Exceeding the maximum amount of remote processes.");
                data = new ProcessUpdateData(false, name);
            }
            else
            {
                EndPoint endpoint;

                if (this.EndpointLookup.ContainsKey(name))
                {
                    this.Logger.Nice("Process", ConsoleColor.Yellow, $"Overriding a remote process ({name})");
                    endpoint = this.EndpointLookup[name];
                    this.EndProcess(endpoint);
                }

                endpoint = state.RemoteEndPoint;
                state.Name = name;
                this.States.Add(endpoint, state);
                this.EndpointLookup.Add(name, endpoint);
                state.Register();
                this.Logger.Nice("Process", ConsoleColor.Magenta, $"Registering new remote process | {name} @ {endpoint}");
                data = new ProcessUpdateData(true, name);
            }

            await this.BroadcastMessageAsync(MessageConstants.REGISTER_IDENTIFIER, data.Serialize());
        }

        private async Task EndProcessAsync(string name, string token)
        {
            ProcessUpdateData data;
            if (token != Config.Instance.Token)
            {
                this.Logger.Warning($"Attempt to end a remote process ({name}) with an invalid token.");

                data = new ProcessUpdateData(false, name);
                await this.BroadcastMessageAsync(MessageConstants.END_IDENTIFIER, data.Serialize());
            }
            else if (!this.EndpointLookup.ContainsKey(name))
            {
                this.Logger.Warning($"Attempt to end a non-existing remote process ({name}). Discarding.");

                data = new ProcessUpdateData(false, name);
                await this.BroadcastMessageAsync(MessageConstants.END_IDENTIFIER, data.Serialize());
            }
            else
            {
                data = new ProcessUpdateData(true, name);
                await this.BroadcastMessageAsync(MessageConstants.END_IDENTIFIER, data.Serialize());

                EndPoint endpoint = this.EndpointLookup[name];
                ClientState state = this.States[endpoint];
                state.Dispose();
                this.States.Remove(endpoint);
                this.EndpointLookup.Remove(name);

                this.Logger.Nice("Process", ConsoleColor.Magenta, $"Ending remote process | {name} @ {endpoint}");
            }
        }

        // When client closes brutally
        private void EndProcess(EndPoint endpoint)
        {
            if(this.States.ContainsKey(endpoint))
            {
                ClientState state = this.States[endpoint];
                state.Dispose();
                this.States.Remove(endpoint);
                this.EndpointLookup.Remove(state.Name);

                this.Logger.Nice("Process", ConsoleColor.Magenta, $"Ending remote process | {state.Name} @ {endpoint}");
            }
        }

        private async Task ForwardMessageAsync(Message msg)
        {
            EndPoint endpoint = this.EndpointLookup[msg.TargetName];
            ClientState state = this.States[endpoint];
            msg.Status = MessageStatus.Success;
            await this.SendAsync(state, msg);

            string tail;
            switch(msg.Type)
            {
                case MessageType.Request:
                    tail = $"| (ID: {msg.Identifier}) {msg.OriginName} -> {msg.TargetName}";
                    this.Logger.Nice("Message", ConsoleColor.Gray, $"Forwarded request {tail}");
                    break;
                case MessageType.Response:
                    tail = $"| (ID: {msg.Identifier}) {msg.TargetName} <- {msg.OriginName}";
                    this.Logger.Nice("Message", ConsoleColor.Gray, $"Forwarded response {msg.Length} bytes {tail}");
                    break;
                case MessageType.Unknown:
                    tail = $"| (ID: {msg.Identifier}) {msg.OriginName} ?? {msg.TargetName}";
                    this.Logger.Nice("Message", ConsoleColor.Yellow, $"Forwarded unknown message type {msg.Length} bytes {tail}");
                    break;
            }
        }

        private async Task AnswerMessageAsync(ClientState state, Message msg, string data = null, MessageStatus status = MessageStatus.Success) 
        {
            Message replyMsg = this.MessageFactory.CreateMessageResponse(msg, data, status);
            await this.SendAsync(state, replyMsg);
        }

        private async Task BroadcastMessageAsync(string identifier, string data)
        {
            foreach(KeyValuePair<EndPoint,ClientState> state in this.States)
            {
                Message broadcastMsg = this.MessageFactory.CreateMessage(identifier, MessageConstants.SERVER_PROCESS_NAME, state.Value.Name, 
                    data, MessageType.Response, MessageStatus.Success);
                await this.SendAsync(state.Value, broadcastMsg);
            }
        }

        private async Task DispatchMessageAsync(ClientState state, Message msg)
        {
            if (msg.IsMalformed)
            {
                this.Logger.Warning($"Malformed message received!\n{msg.Data}");
                return;
            }

            if (this.EndpointLookup.ContainsKey(msg.TargetName) && this.EndpointLookup.ContainsKey(msg.OriginName))
            {
                await this.ForwardMessageAsync(msg);
                return;
            }
            else
            {
                if (!this.EndpointLookup.ContainsKey(msg.OriginName))
                {
                    this.Logger.Warning($"Unknown remote process {msg.OriginName} tried to forward {msg.Length} bytes to {msg.TargetName}");
                }
                else
                {
                    this.Logger.Warning($"{msg.OriginName} tried to forward {msg.Length} bytes to unknown remote process {msg.TargetName}");
                    await this.AnswerMessageAsync(state, msg, null, MessageStatus.ProcessNotFound);
                }
            }
        }
    }
}