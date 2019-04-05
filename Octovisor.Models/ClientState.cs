using System;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Octovisor.Models
{
    public class ClientState : IDisposable
    {
        public const int BufferSize = 256;

        public TcpClient     Client       { get; set; }
        public byte[]        Buffer       { get; }
        public StringBuilder Builder      { get; }
        public string        Identifier   { get; set; }
        public bool          IsDisposed   { get; private set; }
        public int           ParsingDepth { get; set; }

        public EndPoint RemoteEndPoint { get => this.Client.Client.RemoteEndPoint; }

        public ClientState(TcpClient client)
        {
            this.ParsingDepth = 0;
            this.IsDisposed   = false;
            this.Client       = client;
            this.Buffer       = new byte[BufferSize];
            this.Builder      = new StringBuilder();
        }

        public void Dispose()
        {
            this.IsDisposed = true;
            this.Client.Close();
            this.Client.Dispose();
        }
    }
}
