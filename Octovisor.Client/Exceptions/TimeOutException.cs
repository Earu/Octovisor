using System;
using System.Collections.Generic;
using System.Text;

namespace Octovisor.Client.Exceptions
{
    public class TimeOutException : Exception
    {
        public TimeOutException()
        {
            this.Message = "The server or target remote process took too long to respond";
        }

        public override string Message { get; }
    }
}
