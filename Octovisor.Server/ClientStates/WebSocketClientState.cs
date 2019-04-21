using System.Threading.Tasks;
using Fleck;
using Octovisor.Messages;

namespace Octovisor.Server.ClientStates
{
    internal class WebSocketClientState : BaseClientState
    {
        private readonly IWebSocketConnection Connection;

        internal WebSocketClientState(IWebSocketConnection connection) : base()
        {
            this.Connection = connection;
        }

        internal override async Task SendAsync(Message msg)
        {
            string smsg = msg.Serialize() + Config.MessageFinalizer;
            await this.Connection.Send(smsg);
        }

        public override void Dispose()
        {
            base.Dispose();
            this.Connection.Close();
        }
    }
}
