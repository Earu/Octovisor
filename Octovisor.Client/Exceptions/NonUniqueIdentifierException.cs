using System;

namespace Octovisor.Client.Exceptions
{
    public class NonUniqueIdentifierException : Exception
    {
        public NonUniqueIdentifierException(string identifier)
        {
            this.Message = $"Non-unique identifier \'{identifier}\'";
        }

        public override string Message { get; }
    }
}
