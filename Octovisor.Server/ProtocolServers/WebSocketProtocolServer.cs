using Fleck;
using Octovisor.Messages;
using Octovisor.Server.ClientStates;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octovisor.Server.ProtocolServers
{
    internal class WebSocketProtocolServer : BaseProtocolServer
    {
        private readonly WebSocketServer Server;
        private readonly ConcurrentDictionary<Guid, WebSocketClientState> States;

        internal WebSocketProtocolServer(Logger logger, Dispatcher dispatcher) : base(logger, dispatcher)
        {
            FleckLog.LogAction = (level, log, ex) => logger.LogTo("fleck.log", $"{log} | {ex}");
            this.Server = new WebSocketServer($"ws://0.0.0.0:{Config.Instance.WebSocketPort}");
            this.States = new ConcurrentDictionary<Guid, WebSocketClientState>();
        }

        internal override Task RunAsync()
        {
            this.Server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    WebSocketClientState state = new WebSocketClientState(socket);
                    this.States.AddOrUpdate(socket.ConnectionInfo.Id, state, (_, __) => state);
                };

                socket.OnClose = () =>
                {
                    this.States.Remove(socket.ConnectionInfo.Id, out WebSocketClientState _);
                };

                socket.OnMessage = async wsMsg =>
                {
                    if (this.States.ContainsKey(socket.ConnectionInfo.Id))
                        await this.OnMessage(this.States[socket.ConnectionInfo.Id], wsMsg);
                };
            });

            this.Logger.Nice("Web Socket Server", ConsoleColor.Magenta, $"Running on {this.Server.Location}");

            return Task.CompletedTask;
        }

        internal override Task StopAsync()
        {
            this.Server.Dispose();

            return Task.CompletedTask;
        }

        private async Task OnMessage(WebSocketClientState state, string wsMessage)
        {
            Message msg = Message.Deserialize(wsMessage);
            await this.Dispatcher.HandleMessageAsync(state, msg);
        }
    }
}
