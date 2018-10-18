using System;
using System.Net;

namespace Octovisor.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "OctovisorServer";

            OctovisorServer server = new OctovisorServer(Dns.GetHostName(),1100);
            server.Run();
        }
    }
}
