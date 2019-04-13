using Octovisor.Client;
using System;
using System.Threading.Tasks;

namespace Octovisor.Tests.Client2
{
    class Program
    {
        static void Main() 
            => MainAsync().GetAwaiter().GetResult();
        static async Task MainAsync()
        {
            Console.Title = "Octovisor Client";

            Config config = new Config
            {
                Token = "you're cool",
                Address = "127.0.0.1",
                Port = 6558,
                ProcessName = "Meta2",
            };

            OctoClient client = new OctoClient(config);
            client.Log += log => Console.WriteLine(log);

            await client.ConnectAsync();
            client.OnTransmission<string, string>("meme", (proc, data) =>
            {
                return "no u";
            });

            await Task.Delay(-1);
        }
    }
}
