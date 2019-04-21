using Octovisor.Messages;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Octovisor.Server.ClientStates
{
    internal abstract class BaseClientState : IDisposable
    {
        protected BaseClientState()
        {
            this.IsDisposed = false;
            this.IsRegistered = false;
        }

        internal string Name { get; set; }
        protected internal bool IsDisposed { get; protected set; }
        protected internal bool IsRegistered { get; protected set; }

        internal void Register()
            => this.IsRegistered = true;

        internal abstract Task SendAsync(Message msg);

        public virtual void Dispose()
        {
            this.IsDisposed = true;
            this.IsRegistered = false;
        }
    }
}
