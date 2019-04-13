using System;

namespace Octovisor.Client.Exceptions
{
    public class UnconnectedException : Exception
    {
        public UnconnectedException()
        {
            this.Message = "Not connected to server";
        }

        public override string Message { get; }
    }
}
