namespace Octovisor.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            OctovisorServer server = new OctovisorServer();
            server.Run();
        }
    }
}
