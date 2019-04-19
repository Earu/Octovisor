using System.Threading.Tasks;

namespace Octovisor.Server.Servers
{
    internal class NamedPipeServer : BaseProtocolServer
    {
        public NamedPipeServer(Logger logger, Dispatcher dispatcher) : base(logger, dispatcher)
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
