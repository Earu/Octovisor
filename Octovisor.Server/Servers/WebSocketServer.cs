using Octovisor.Messages;
using Octovisor.Server.Clients;
using Octovisor.Server.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

        private string ComputeHandshakeHash(string data)
        {
            string match = new Regex("Sec-WebSocket-Key: (.*)").Match(data).Groups[1].Value.Trim();
            byte[] bytes = Encoding.UTF8.GetBytes($"{match}258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
            return Convert.ToBase64String(SHA1.Create().ComputeHash(bytes));
        }

        private async Task DoHandshakeAsync(WebSocketClientState state)
        {
            if (state.HasHandshaked) return;

            while (state.Client.Available < 3)
                await Task.Delay(250);

            byte[] buffer = new byte[state.Client.Available];
            await state.Stream.ReadAsync(buffer, 0, buffer.Length);
            string data = Encoding.UTF8.GetString(buffer);
            if (data.StartsWith("GET"))
            {
                string eol = "\r\n";
                string response = "HTTP/1.1 101 Switching Protocols" + eol
                    + "Connection: Upgrade" + eol
                    + "Upgrade: websocket" + eol
                    + $"Sec-WebSocket-Accept: " + this.ComputeHandshakeHash(data) + eol + eol;
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await state.Stream.WriteAsync(responseBytes);
            }
        }

        private byte[] DecodeBytes(byte[] encoded)
        {
            byte[] decoded = new byte[encoded.Length];
            byte[] mask = new byte[4] { 61, 84, 35, 6 };

            for (int i = 0; i < encoded.Length; i++)
                decoded[i] = (byte)(encoded[i] ^ mask[i % 4]);

            return decoded;
        }

        private async Task ListenAsync(WebSocketClientState state)
        {
            try
            {
                Stream stream = state.Stream;
                int bytesRead = await stream.ReadAsync(state.Buffer);
                if (bytesRead <= 0) return;

                string data = Encoding.UTF8.GetString(state.Buffer);

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
