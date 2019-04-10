using Octovisor.Client;
using Octovisor.Models;
using System;
using System.Threading.Tasks;

namespace Octovisor.Tests.Client
{
    class Program
    {
        static void Main() => MainAsync().GetAwaiter().GetResult();
        static async Task MainAsync()
        {
            Console.Title = "Octovisor Client";

            Config config = new Config
            {
                Token = "you're cool",
                Address = "127.0.0.1",
                Port = 6558,
                ProcessName = "TestProcess",
            };

            BaseClient client = new OctoClient(config);
            client.ExceptionThrown += e => Console.WriteLine(e);
            client.Log += log => Console.WriteLine(log);

            await client.ConnectAsync();
            client.

            await Task.Delay(-1);
        }
    }
}
