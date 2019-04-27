using Octovisor.Client;
using Octovisor.Debugger.Windows;
using System;

namespace Octovisor.Debugger
{
    public class ExecutionContext
    {
        private readonly DebuggingWindow Window;

        public ExecutionContext(DebuggingWindow win, OctoClient client)
        {
            this.Window = win;
            this.Client = client;
        }

        public OctoClient Client { get; private set; }

        public void Print(object value)
            => this.Window.PrintLine(value.ToString());

        public void Error(Exception ex)
            => this.Window.PrintException(ex);
    }
}
