using System;

namespace Octovisor.Client.Exceptions
{
    public class ReservedIdentifierException : Exception
    {
        public ReservedIdentifierException(string identifier)
        {
            string msg = $"\'{identifier}\' is a reserved Octovisor identifier";
            this.Message = msg;
        }

        public override string Message { get; }
    }
}
