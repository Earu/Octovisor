using System.Threading.Tasks;

namespace Octovisor.Server.Servers
{
    internal class WebSocketServer : BaseProtocolServer
    {
        public WebSocketServer(Logger logger, Dispatcher dispatcher) : base(logger, dispatcher)
        {
        }

        internal override Task RunAsync()
            => Task.CompletedTask;

        internal override Task StopAsync()
            => Task.CompletedTask;
    }
}
