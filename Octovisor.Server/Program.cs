using System.Net;

namespace Octovisor.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            OctovisorServer server = new OctovisorServer(Dns.GetHostName(),1100);
            server.Run();
        }
    }
}
