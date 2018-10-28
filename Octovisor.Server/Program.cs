using System;
using Octovisor.Models;

namespace Octovisor.Server
{
    class Program
    {
        static void Main()
        {
            Console.Title = "OctovisorServer";

            ServerConfig config = new ServerConfig
            {
                ServerAddress = "127.0.0.1",
                ServerPort = 1100,
                Token = "MEMES"
            };

            OctovisorServer server = new OctovisorServer(config);
            server.Run();
        }
    }
}
