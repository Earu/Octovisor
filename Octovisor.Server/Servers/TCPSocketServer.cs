using Octovisor.Messages;
using Octovisor.Server.Clients;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Octovisor.Server.Servers
{
    internal class TCPSocketServer : BaseProtocolServer
    {
        private Task InternalTask;
        private bool ShouldRun;
        private TcpListener Listener;

        public TCPSocketServer(Logger logger, Dispatcher dispatcher) : base(logger, dispatcher)
        {
            this.ShouldRun = false;
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
                IPEndPoint endpoint = new IPEndPoint(IPAddress.IPv6Any, Config.Instance.Port);
                TcpListener listener = new TcpListener(endpoint);
                // To accept IPv4 and IPv6
                listener.Server.DualMode = true;
                listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, 0);
                listener.Start(Config.Instance.MaxProcesses);

                this.Listener = listener;
                this.Logger.Nice("TCP Server", ConsoleColor.Magenta, $"Running on {endpoint}...");

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
            TcpClient client = null;
            try
            {
                client = await this.Listener.AcceptTcpClientAsync();
            }
            catch (Exception e)
            {
                await this.OnExceptionAsync(e);
            }

            if (client == null) return;
            TCPSocketClientState state = new TCPSocketClientState(client);

#pragma warning disable CS4014
            this.ListenAsync(state);
#pragma warning restore CS4014
        }

        private bool ShouldHandleException(Exception ex)
        {
            if (ex is SocketException sEx && sEx.SocketErrorCode == SocketError.ConnectionReset)
                return true;
            else if (ex is IOException)
                return true;

            if (ex.InnerException == null) return false;

            ex = ex.InnerException;
            if (ex is SocketException sExInner && sExInner.SocketErrorCode == SocketError.ConnectionReset)
                return true;
            else if (ex is IOException)
                return true;

            return false;
        }

        private async Task HandleExceptionAsync(Exception ex, Func<Task> onConnectionReset)
        {
            if (this.ShouldHandleException(ex))
                await onConnectionReset();
            else
                this.Logger.Error(ex.ToString());
        }

        private async Task OnClientStateExceptionAsync(TCPSocketClientState state, Exception e)
        {
            await this.HandleExceptionAsync(e, (async () =>
            {
                this.Logger.Nice("Process", ConsoleColor.Red, $"Remote process \'{state.Name}\' was forcibly closed");
                this.Dispatcher.TerminateProcess(state.Name);

                ProcessUpdateData enddata = new ProcessUpdateData(true, state.Name);
                await this.Dispatcher.BroadcastMessageAsync(MessageConstants.END_IDENTIFIER, enddata.Serialize());
            }));
        }

        private async Task OnExceptionAsync(Exception e)
        {
            await this.HandleExceptionAsync(e, () =>
            {
                this.Logger.Nice("Process", ConsoleColor.Red, "A remote process was forcibly closed when connecting");
                return Task.CompletedTask;
            });
        }

        private async Task ListenAsync(TCPSocketClientState state)
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
                await this.OnClientStateExceptionAsync(state, e);
            }
        }
    }
}
