using System.Threading.Tasks;

namespace Octovisor.Server
{
    internal class Program
    {
        private static void Main(string[] args)
            => MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();

        private static async Task MainAsync(string[] args)
        {
            string configpath = args.Length > 0 ? args[0] : "config.yaml";
            Config.Initialize(configpath);
            Server server = new Server();
            await server.RunAsync();
        }
    }
}
