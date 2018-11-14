using System;
using System.Net.Sockets;
using System.Text;

namespace Octovisor.Models
{
    public class StateObject : IDisposable
    {
        public const int BufferSize = 256;

        public Socket        WorkSocket   { get; set; }
        public byte[]        Buffer       { get; }
        public StringBuilder Builder      { get; }
        public string        Identifier   { get; set; }
        public bool          IsDisposed   { get; private set; }
        public int           ParsingDepth { get; set; }

        public StateObject(Socket socket)
        {
            this.ParsingDepth = 0;
            this.IsDisposed   = false;
            this.WorkSocket   = socket;
            this.Buffer       = new byte[BufferSize];
            this.Builder      = new StringBuilder();
        }

        public void Dispose()
        {
            this.IsDisposed = true;
            this.WorkSocket.Shutdown(SocketShutdown.Both);
            this.WorkSocket.Close();
        }
    }
}
