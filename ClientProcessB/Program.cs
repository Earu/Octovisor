using Octovisor.Client;

namespace Octovisor.Tests.ClientProcessB
{
    public class Program
    {
        public static int Main(string[] args)
        {
            OctovisorClient client = new OctovisorClient();
            client.Run();

            return 0;
        }
    }
}