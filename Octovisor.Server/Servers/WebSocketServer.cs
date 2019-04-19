using Octovisor.Messages;
using Octovisor.Server.Clients;
using Octovisor.Server.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Octovisor.Server.Servers
{
    internal class WebSocketServer : BaseProtocolServer
    {
        private Task InternalTask;
        private bool ShouldRun;
        private TcpListener Listener;

        private readonly SocketExceptionHandler ExceptionHandler;

        public WebSocketServer(Logger logger, Dispatcher dispatcher) : base(logger, dispatcher)
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
                IPEndPoint endpoint = new IPEndPoint(IPAddress.IPv6Any, Config.Instance.WebSocketPort);
                TcpListener listener = new TcpListener(endpoint);
                // To accept IPv4 and IPv6
                listener.Server.DualMode = true;
                listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, 0);
                listener.Start(Config.Instance.MaxProcesses);

                this.Listener = listener;
                this.Logger.Nice("Web Socket Server", ConsoleColor.Magenta, $"Running on {endpoint}...");

                while (this.ShouldRun)
                    await this.ListenConnectionAsync();
            }
            catch (Exception e)
            {
                this.Logger.LogTo("web_socket_crashes.txt", e.ToString());
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
                await this.ExceptionHandler.OnExceptionAsync(e);
            }

            if (client == null) return;
            WebSocketClientState state = new WebSocketClientState(client);

#pragma warning disable CS4014
            this.ListenAsync(state);
#pragma warning restore CS4014
        }

        private async Task ListenAsync(WebSocketClientState state)
        {
            try
            {
                Stream stream = state.Stream;
                int bytesRead = await stream.ReadAsync(state.Buffer);
                if (bytesRead <= 0) return;

                string data = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);

                List<Message> msgs = state.Reader.Read(data);
                state.ClearBuffer();

                await this.Dispatcher.HandleMessagesAsync(state, msgs);


            }
            catch (Exception e)
            {
                await this.ExceptionHandler.OnClientStateExceptionAsync(state, e);
            }
        }
    }
}
