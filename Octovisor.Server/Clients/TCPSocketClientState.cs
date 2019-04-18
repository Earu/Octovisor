using Octovisor.Messages;
using System;
using System.Net;
using System.Net.Sockets;

namespace Octovisor.Server.Clients
{
    internal class TCPSocketClientState : BaseClientState
    {
        internal const int BufferSize = 256;

        internal TcpClient Client { get; set; }
        internal byte[] Buffer { get; private set; }
        internal MessageReader Reader { get; }
        internal EndPoint RemoteEndPoint { get => this.Client.Client.RemoteEndPoint; }

        internal TCPSocketClientState(TcpClient client) : base()
        {
            this.Client = client;
            this.Buffer = new byte[BufferSize];
            this.Reader = new MessageReader(Config.Instance.MessageFinalizer);
            this.Stream = this.Client.GetStream();
        }

        public void ClearBuffer()
            => Array.Clear(this.Buffer, 0, this.Buffer.Length);

        public override void Dispose()
        {
            base.Dispose();
            this.Client.Close();
            this.Client.Dispose();
            this.ClearBuffer();
            this.Reader.Clear();
        }
    }
}
