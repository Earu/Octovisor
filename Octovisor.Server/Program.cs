﻿using System.Threading.Tasks;

namespace Octovisor.Server
{
    internal class Program
    {
        private static void Main(string[] args)
            => MainAsync(args).ConfigureAwait(true).GetAwaiter().GetResult();

        private static async Task MainAsync(string[] args)
        {
            string configPath = args.Length > 0 ? args[0] : "config.yaml";
            Config.Initialize(configPath);
            OctoServer server = new OctoServer();
            await server.RunAsync();
        }
    }
}
