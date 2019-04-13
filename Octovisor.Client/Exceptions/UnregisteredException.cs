using System;

namespace Octovisor.Client.Exceptions
{
    public class UnregisteredException : Exception
    {
        public UnregisteredException()
        {
            this.Message = "Not registered on server";
        }

        public override string Message { get; }
    }
}
