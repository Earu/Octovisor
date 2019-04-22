using Octovisor.Messages;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Octovisor.Server.ClientStates
{
    internal class TCPSocketClientState : BaseClientState
    {
        internal const int BufferSize = 256;

        internal TcpClient Client { get; set; }
        internal byte[] Buffer { get; private set; }
        internal MessageReader Reader { get; }
        internal EndPoint RemoteEndPoint { get => this.Client.Client.RemoteEndPoint; }
        internal NetworkStream Stream { get; private set; }

        internal TCPSocketClientState(TcpClient client) : base()
        {
            this.Client = client;
            this.Buffer = new byte[BufferSize];
            this.Reader = new MessageReader(Config.MessageFinalizer);
            this.Stream = this.Client.GetStream();
        }

        public void ClearBuffer()
            => Array.Clear(this.Buffer, 0, this.Buffer.Length);

        internal override async Task SendAsync(Message msg)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(msg.Serialize() + Config.MessageFinalizer);
            await this.Stream.WriteAsync(bytes);
            await this.Stream.FlushAsync();
        }

        public override void Dispose()
        {
            base.Dispose();
            this.Stream.Dispose();
            this.Client.Close();
            this.Client.Dispose();
            this.ClearBuffer();
            this.Reader.Clear();
        }
    }
}
