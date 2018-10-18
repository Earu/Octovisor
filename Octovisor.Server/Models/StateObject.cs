using System.Net.Sockets;
using System.Text;

namespace Octovisor.Server.Models
{
    internal class StateObject
    {
        internal const int BufferSize = 256;

        internal Socket WorkSocket { get; }
        internal byte[] Buffer { get; }
        internal StringBuilder Builder { get; }

        internal StateObject(Socket socket)
        {
            this.WorkSocket = socket;
            this.Buffer = new byte[BufferSize];
            this.Builder = new StringBuilder();
        }
    }
}
