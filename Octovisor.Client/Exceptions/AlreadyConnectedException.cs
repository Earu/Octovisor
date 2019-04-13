using System;

namespace Octovisor.Client.Exceptions
{
    public class AlreadyConnectedException : Exception
    {
        public AlreadyConnectedException()
        {
            this.Message = "Already connected to server";
        }

        public override string Message { get; }
    }
}
