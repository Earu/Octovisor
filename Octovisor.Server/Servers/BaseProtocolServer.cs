using System.Threading.Tasks;

namespace Octovisor.Server.Servers
{
    internal abstract class BaseProtocolServer
    {
        protected readonly Logger Logger;
        protected readonly string MessageFinalizer;
        protected readonly Dispatcher Dispatcher;
        internal BaseProtocolServer(Logger logger, Dispatcher dispatcher)
        {
            this.Logger = logger;
            this.MessageFinalizer = Config.Instance.MessageFinalizer;
            this.Dispatcher = dispatcher;
        }

        internal abstract Task RunAsync();

        internal abstract Task StopAsync();
    }
}
