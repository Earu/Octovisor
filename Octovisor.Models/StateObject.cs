using System.Net.Sockets;
using System.Text;

namespace Octovisor.Models
{
    public class StateObject
    {
        public const int BufferSize = 256;

        public Socket WorkSocket { get; }
        public byte[] Buffer { get; }
        public StringBuilder Builder { get; }

        public StateObject(Socket socket)
        {
            this.WorkSocket = socket;
            this.Buffer = new byte[BufferSize];
            this.Builder = new StringBuilder();
        }
    }
}
