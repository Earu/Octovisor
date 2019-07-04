using Octovisor.Messages;
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Octovisor.Server.ClientStates
{
    internal class TCPSocketClientState : BaseClientState
    {
        internal const int BufferSize = 256;

        internal TcpClient Client { get; set; }
        internal byte[] Buffer { get; }
        internal MessageReader Reader { get; }
        internal EndPoint RemoteEndPoint => this.Client.Client.RemoteEndPoint;
        internal NetworkStream Stream { get; }

        internal TCPSocketClientState(TcpClient client) 
        {
            this.Client = client;
            this.Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            this.Buffer = new byte[BufferSize];
            this.Reader = new MessageReader(Config.MessageFinalizer);
            this.Stream = this.Client.GetStream();
        }

        internal void ClearBuffer()
            => Array.Clear(this.Buffer, 0, BufferSize);

        internal TcpState GetTcpState()
        {
            TcpConnectionInformation conInfo = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpConnections()
                .SingleOrDefault(con => con.LocalEndPoint.Equals(this.Client.Client.LocalEndPoint));

            return conInfo?.State ?? TcpState.Unknown;
        }

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
