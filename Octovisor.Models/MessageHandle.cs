using System;
using System.Threading.Tasks;

namespace Octovisor.Models
{
    public class MessageHandle
    {
        public Type ReturnType { get; private set; }

        public TaskCompletionSource<bool> TCS { get; private set; }

        public MessageHandle(Type type, TaskCompletionSource<bool> tcs)
        {
            this.ReturnType = type;
            this.TCS = tcs;
        }
    }
}
