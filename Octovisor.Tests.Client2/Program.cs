using Octovisor.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octovisor.Tests.Client2
{
    class TestClass
    {
        public string Lol;
        public int What;
        public long Sure;
        public List<(int, byte[])> Ok;
    }

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
                ProcessName = "Process2",
            };

            OctoClient client = new OctoClient(config);
            await client.ConnectAsync();

            foreach (RemoteProcess proc in client.AvailableProcesses)
                Console.WriteLine(proc.Name);

            client.OnTransmission<string, string>("meme", (proc, data) =>
            {
                Console.WriteLine(proc.Name);
                Console.WriteLine(data);

                return "hello world";
            });

            await Task.Delay(-1);
        }
    }
}
