using System.Threading.Tasks;

namespace Octovisor.Server.Servers
{
    internal class NamedPipeServer : BaseProtocolServer
    {
        public NamedPipeServer(Logger logger, Dispatcher dispatcher) : base(logger, dispatcher)
        {
        }

        internal override Task RunAsync()
            => Task.CompletedTask;

        internal override Task StopAsync()
            => Task.CompletedTask;
    }
}
