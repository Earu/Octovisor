using Octovisor.Messages;
using Octovisor.Server.ClientStates;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Octovisor.Server.Utilities
{
    internal class SocketExceptionHandler
    {
        private readonly Logger Logger;
        private readonly Dispatcher Dispatcher;

        internal SocketExceptionHandler(Logger logger, Dispatcher dispatcher)
        {
            this.Logger = logger;
            this.Dispatcher = dispatcher;
        }

        private bool ShouldHandleException(Exception ex)
        {
            if (ex is SocketException sEx && sEx.SocketErrorCode == SocketError.ConnectionReset)
                return true;
            else if (ex is IOException)
                return true;

            if (ex.InnerException == null) return false;

            ex = ex.InnerException;
            if (ex is SocketException sExInner && sExInner.SocketErrorCode == SocketError.ConnectionReset)
                return true;
            else if (ex is IOException)
                return true;

            return false;
        }

        private async Task HandleExceptionAsync(Exception ex, Func<Task> onConnectionReset)
        {
            if (this.ShouldHandleException(ex))
                await onConnectionReset();
            else
                this.Logger.Error(ex.ToString());
        }

        internal async Task OnClientStateExceptionAsync(TCPSocketClientState state, Exception ex)
        {
            await this.HandleExceptionAsync(ex, async () =>
            {
                this.Dispatcher.TerminateProcess(state.Name);

                ProcessUpdateData enddata = new ProcessUpdateData(true, state.Name);
                await this.Dispatcher.BroadcastMessageAsync(MessageConstants.TERMINATE_IDENTIFIER, enddata.Serialize());
            });
        }

        internal async Task OnExceptionAsync(Exception ex) // Happens when a process disconnects when connecting
            => await this.HandleExceptionAsync(ex, () =>
            {
                this.Logger.Danger(ex);
                return Task.CompletedTask;
            });
    }
}
