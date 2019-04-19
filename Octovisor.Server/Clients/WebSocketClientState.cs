using System.Net.Sockets;

namespace Octovisor.Server.Clients
{
    internal class WebSocketClientState : TCPSocketClientState
    {
        internal WebSocketClientState(TcpClient client) : base(client)
        {
            this.HasHandshaked = false;
        }

        internal bool HasHandshaked { get; set; }
    }
}
