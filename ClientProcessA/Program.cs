using Octovisor.Client;

namespace Octovisor.Tests.ClientProcessA
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
