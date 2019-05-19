using Octovisor.Messages;
using Octovisor.Server.ClientStates;
using Octovisor.Server.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
                Stream stream = state.Stream;
                await stream.ReadAsync(state.Buffer);
                if (state.Buffer.Length <= 0) return;

                string data = Encoding.UTF8.GetString(state.Buffer, 0, TCPSocketClientState.BufferSize);
                state.ClearBuffer();
                this.Logger.Nice("TCP", ConsoleColor.Gray, $"Received {data.Length} bytes");
                List<Message> msgs = state.Reader.Read(data);

                await this.Dispatcher.HandleMessagesAsync(state, msgs);

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
                await this.ExceptionHandler.OnClientStateExceptionAsync(state, e);
            }
        }
    }
}
