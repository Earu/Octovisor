using Octovisor.Messages;
using Octovisor.Server.ClientStates;
using Octovisor.Server.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Octovisor.Server.ProtocolServers
{
    internal class TCPSocketProtocolServer : BaseProtocolServer
    {
        private Task InternalTask;
        private bool ShouldRun;
        private TcpListener Listener;

        private readonly SocketExceptionHandler ExceptionHandler;

        public TCPSocketProtocolServer(Logger logger, Dispatcher dispatcher) : base(logger, dispatcher)
        {
            this.ShouldRun = false;
            this.ExceptionHandler = new SocketExceptionHandler(logger, dispatcher);
        }

        internal override async Task RunAsync()
        {
            if (this.InternalTask != null)
                await this.StopAsync();

            this.ShouldRun = true;
            this.InternalTask = this.InternalRunAsync();

            await this.InternalTask;
        }

        internal override async Task StopAsync()
        {
            if (this.InternalTask == null) return;

            this.ShouldRun = false;
            await this.InternalTask;
        }

        private async Task InternalRunAsync()
        {
            try
            {
                IPEndPoint endpoint = new IPEndPoint(IPAddress.IPv6Any, Config.Instance.TCPSocketPort);
                TcpListener listener = new TcpListener(endpoint);
                // To accept IPv4 and IPv6
                listener.Server.DualMode = true;
                listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, 0);
                listener.Start(Config.Instance.MaxProcesses);

                this.Listener = listener;
                this.Logger.Nice("TCP Socket Server", ConsoleColor.Magenta, $"Running on {endpoint}...");

                while (this.ShouldRun)
                    await this.ListenConnectionAsync();
            }
            catch (Exception e)
            {
                this.Logger.Nice("TCP Socket Server", ConsoleColor.Red, "Critical state, shutting down TCP coms");
                this.Dispatcher.TerminateProcesses<TCPSocketClientState>();
                this.Logger.LogTo("tcp_socket_crashes.txt", e.ToString());
            }
        }

        private async Task ListenConnectionAsync()
        {
            try
            {
                TcpClient client = await this.Listener.AcceptTcpClientAsync();
                TCPSocketClientState state = new TCPSocketClientState(client);

#pragma warning disable CS4014
                this.ListenAsync(state);
#pragma warning restore CS4014
            }
            catch (Exception e)
            {
                await this.ExceptionHandler.OnExceptionAsync(e);
            }
        }

        private async Task ListenAsync(TCPSocketClientState state)
        {
            try
            {
                do
                {
                    Stream stream = state.Stream;
                    int bytesRead = await stream.ReadAsync(state.Buffer);
                    if (bytesRead <= 0)
                    {
                        TcpState tcpState = state.GetTcpState();
                        if (tcpState != TcpState.Established)
                        {
                            this.Dispatcher.TerminateProcess(state.Name);
                            ProcessUpdateData updateData = new ProcessUpdateData(true, state.Name);
                            await this.Dispatcher.BroadcastMessageAsync(MessageConstants.TERMINATE_IDENTIFIER, updateData.Serialize());

                            return;
                        }

                        state.ClearBuffer();
                        await Task.Delay(10);
                        continue;
                    }

                    string data = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);
                    state.ClearBuffer();
                    List<Message> msgs = state.Reader.Read(data);

                    await this.Dispatcher.HandleMessagesAsync(state, msgs);

                    // Maximum register payload size is 395 bytes, the client is sending garbage.
                    if (!state.IsRegistered && state.Reader.Size >= 600)
                        state.Reader.Clear();
                }
                while (!state.IsDisposed && state.IsRegistered);
            }
            catch (Exception e)
            {
                await this.ExceptionHandler.OnClientStateExceptionAsync(state, e);
            }
        }
    }
}
