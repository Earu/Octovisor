using Octovisor.Models;
using Octovisor.Server;
using System;

namespace Octovisor.Tests.Server
{
    class Program
    {
        static void Main()
        {
            Console.Title = "Octovisor Server";

            ServerConfig config = new ServerConfig
            {
                Token = "MetaCosntructIsCool",
                ServerPort = 1100,
                MaximumProcesses = 255,
            };

            OctovisorServer server = new OctovisorServer(config);
            server.Run();
        }
    }
}
