namespace Octovisor.Server.Servers
{
    internal class NamedPipeServer : BaseProtocolServer
    {
        public NamedPipeServer(Logger logger, Dispatcher dispatcher) : base(logger, dispatcher)
        {
        }
    }
}
