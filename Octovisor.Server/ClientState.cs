using Octovisor.Messages;
using System;
using System.Net;
using System.Net.Sockets;

namespace Octovisor.Server
{
    public class ClientState : IDisposable
    {
        public const int BufferSize = 256;

        public TcpClient     Client       { get; set; }
        public byte[]        Buffer       { get; private set; }
        public MessageReader Reader       { get; }
        public string        Name   { get; set; }
        public bool          IsDisposed   { get; private set; }
        public int           ParsingDepth { get; set; }
        public bool          IsRegistered { get; private set; }

        public EndPoint RemoteEndPoint { get => this.Client.Client.RemoteEndPoint; }

        public ClientState(TcpClient client, string messageFinalizer)
        {
            this.ParsingDepth = 0;
            this.IsDisposed   = false;
            this.Client       = client;
            this.Buffer       = new byte[BufferSize];
            this.Reader       = new MessageReader(messageFinalizer);
            this.IsRegistered = false;
        }

        public void ClearBuffer()
            => Array.Clear(this.Buffer, 0, this.Buffer.Length);

        public void Register()
            => this.IsRegistered = true;

        public void Dispose()
        {
            this.IsDisposed = true;
            this.IsRegistered = false;
            this.Client.Close();
            this.Client.Dispose();
            this.ClearBuffer();
            this.Reader.Clear();
        }
    }
}
