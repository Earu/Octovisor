using System;

namespace Octovisor.Client.Exceptions
{
    public class UnknownRemoteProcessException : Exception
    {
        public UnknownRemoteProcessException(string processName)
        {
            string msg = $"Process \'{processName}\' does not exist or is not available";
            this.Message = msg;
        }

        public override string Message { get; }
    }
}
