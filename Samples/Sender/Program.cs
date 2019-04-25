using Octovisor.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octovisor.Tests.Client
{
    class TestClass
    {
        public string Lol = new string('A', 5000);
        public int What = int.MaxValue;
        public long Sure = long.MaxValue;
        public List<(int, byte[])> Ok = new List<(int, byte[])>
        {
            (4564, new byte[100]),
            (456456464, new byte[3000])
        };
    }

    class Program
    {
        static void Main()
            => MainAsync().GetAwaiter().GetResult();

        static async Task<OctoClient> CreateClientAsync(string procName)
        {
            Config config = new Config
            {
                Token = "you're cool",
                Address = "127.0.0.1",
                Port = 6558,
                ProcessName = procName,
            };

            OctoClient client = new OctoClient(config);
            await client.ConnectAsync();

            return client;

        }

        static async Task SpamRemoteProcessAsync(OctoClient client, string procName)
        {
            var last = DateTime.Now;
            RemoteProcess proc = client.GetProcess(procName);
            for (int i = 0; i < 10; i++)
            {
                string result = await proc.TransmitObjectAsync<TestClass, string>("meme", new TestClass());
                Console.WriteLine($"{client.ProcessName}: {result}");
            }
            Console.WriteLine($"Took {(DateTime.Now - last).TotalMilliseconds}ms");
        }

        static async Task MainAsync()
        {
            var client1 = await CreateClientAsync("Process1");
            //var client2 = await CreateClientAsync("Meta2");
            SpamRemoteProcessAsync(client1, "Process2");
            //SpamRemoteProcessAsync(client2, "Process2");

            await Task.Delay(-1);
        }
    }
}
