using Octovisor.Server.Properties;
using Octovisor.Server.Servers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Octovisor.Server
{
    internal class OctoServer
    {
        private readonly List<BaseProtocolServer> ProtocolServers;
        private readonly List<Thread> ServerThreads;
        internal OctoServer()
        {
            Console.Clear();
            Console.Title = Resources.Title;

            Logger logger = new Logger();
            Dispatcher dispatcher = new Dispatcher(logger);

            this.ServerThreads = new List<Thread>();
            this.ProtocolServers = new List<BaseProtocolServer>
            {
                new TCPSocketServer(logger, dispatcher),
                new WebSocketServer(logger, dispatcher),
                new NamedPipeServer(logger, dispatcher),
                // more server impls here
            };
        }

        private void DisplayAsciiArt()
        {
            ConsoleColor[] colors =
            {
                ConsoleColor.Blue, ConsoleColor.Cyan, ConsoleColor.Green,
                ConsoleColor.Yellow, ConsoleColor.Red, ConsoleColor.Magenta,
            };
            string ascii = Resources.Ascii;
            string[] lines = ascii.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                ConsoleColor col = colors[i];
                Console.ForegroundColor = col;
                Console.WriteLine($"\t{line}");
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n" + new string('-', 70));
        }

        internal async Task RunAsync()
        {
            this.DisplayAsciiArt();
            foreach (BaseProtocolServer server in this.ProtocolServers)
                this.ServerThreads.Add(new Thread(async () => await server.RunAsync()));

            foreach (Thread thread in this.ServerThreads)
                thread.Start();

            await Task.Delay(-1);
        }

        internal async Task StopAsync()
        {
            foreach (BaseProtocolServer server in this.ProtocolServers)
                await server.StopAsync();
        }
    }
}