using Octovisor.Client;
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

            OctoClient client = new OctoClient(config);

            client.ExceptionThrown += e => Console.WriteLine(e);
            client.Log += log => Console.WriteLine(log);

            await client.ConnectAsync();
            using (RemoteProcess proc = client.Use("Meta2"))
                for (int i = 0; i < 10; i++)
                    await proc.TransmitObjectAsync("meme", new string('A', 10000));

            await Task.Delay(-1);
        }
    }
}
