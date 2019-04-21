using Octovisor.Server.Properties;
using Octovisor.Server.ProtocolServers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octovisor.Server
{
    internal class OctoServer
    {
        private readonly List<BaseProtocolServer> ProtocolServers;
        internal OctoServer()
        {
            Console.Clear();
            Console.Title = Resources.Title;

            Logger logger = new Logger();
            Dispatcher dispatcher = new Dispatcher(logger);

            this.ProtocolServers = new List<BaseProtocolServer>
            {
                new TCPSocketProtocolServer(logger, dispatcher),
                new WebSocketProtocolServer(logger, dispatcher),
                new NamedPipeProtocolServer(logger, dispatcher),
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

            List<Task> tasks = new List<Task>();
            foreach (BaseProtocolServer server in this.ProtocolServers)
                tasks.Add(server.RunAsync());

            await Task.WhenAll(tasks);
        }

        internal async Task StopAsync()
        {
            foreach (BaseProtocolServer server in this.ProtocolServers)
                await server.StopAsync();
        }
    }
}