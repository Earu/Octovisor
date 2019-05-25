using System;

namespace Octovisor.Client.Exceptions
{
    public class NotConnectedException : Exception
    {
        public NotConnectedException()
        {
            this.Message = "Not connected to server";
        }

        public override string Message { get; }
    }
}
