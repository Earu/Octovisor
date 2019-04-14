using Octovisor.Client;
using System;
using System.Threading.Tasks;

namespace Octovisor.Tests.Client
{
    class Program
    {
        static void Main()
            => MainAsync().GetAwaiter().GetResult();
        static async Task MainAsync()
        {
            Config config = new Config
            {
                Token = "you're cool",
                Address = "127.0.0.1",
                Port = 6558,
                ProcessName = "TestProcess",
            };

            OctoClient client = new OctoClient(config);
            client.Log += Console.WriteLine;

            await client.ConnectAsync();
            RemoteProcess proc = client.GetProcess("Meta2");
            for (int i = 0; i < 10; i++)
            {
                string result = await proc.TransmitObjectAsync<string, string>("meme", new string('A', 10000));
                Console.WriteLine(result);
            }

            await Task.Delay(-1);
        }
    }
}
