using System.Threading.Tasks;

namespace Octovisor.Server.ProtocolServers
{
    internal class NamedPipeProtocolServer : BaseProtocolServer
    {
        public NamedPipeProtocolServer(Logger logger, Dispatcher dispatcher) : base(logger, dispatcher)
        {
        }

        internal override Task RunAsync()
        {
            this.Logger.Nice("Named Pipe Server", System.ConsoleColor.Magenta, "Successfully started");

            return Task.CompletedTask;
        }

        internal override Task StopAsync()
            => Task.CompletedTask;
    }
}
