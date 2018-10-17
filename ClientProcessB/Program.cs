using Octovisor.Client;

namespace Octovisor.Tests.ClientProcessB
{
    class Program
    {
        static void Main(string[] args)
        {
            OctovisorClient client = new OctovisorClient();
            client.Run();
        }
    }
}
