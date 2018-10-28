using System;

namespace Octovisor.Server
{
    class Program
    {
        static void Main()
        {
            Console.Title = "OctovisorServer";

            OctovisorServer server = new OctovisorServer("127.0.0.1",1100);
            server.Run();
        }
    }
}
