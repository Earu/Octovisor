using System.Threading.Tasks;
using Octovisor.Messages;

namespace Octovisor.Server.ClientStates
{
    internal class NamedPipeClientState : BaseClientState
    {
        internal NamedPipeClientState() : base()
        {
        }

        internal override Task SendAsync(Message msg)
            => Task.CompletedTask;
    }
}
