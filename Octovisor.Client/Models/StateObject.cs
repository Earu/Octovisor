using System.Net.Sockets;
using System.Text;

namespace Octovisor.Client.Models
{
    internal class StateObject
    {
        internal const int BufferSize = 256;

        internal Socket WorkSocket { get; set; }
        internal byte[] Buffer { get; }
        internal StringBuilder Builder { get; }

        internal StateObject()
        {
            this.WorkSocket = null;
            this.Buffer = new byte[BufferSize];
            this.Builder = new StringBuilder();
        }
    }
}
