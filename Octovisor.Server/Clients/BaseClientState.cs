using System;
using System.IO;

namespace Octovisor.Server.Clients
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
        protected internal Stream Stream { get; protected set; }

        internal void Register()
            => this.IsRegistered = true;

        public virtual void Dispose()
        {
            this.IsDisposed = true;
            this.IsRegistered = false;
            this.Stream.Dispose();
        }
    }
}
