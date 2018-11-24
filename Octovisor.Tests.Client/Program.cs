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

            ClientConfig config = new ClientConfig
            {
                Token         = "MetaCosntructIsCool",
                ServerAddress = "earu.io",
                ServerPort    = 6558,
                ProcessName   = "TestProcess",
            };

            OctovisorClient client = new OctovisorClient(config);
            client.OnError += e    => Console.WriteLine(e);
            client.Log     += log  => Console.WriteLine(log);

            await client.Connect();

            // Test spam to test server
            for(int i = 0; i < 100; i++)
                await client.Send(new Message
                {
                    Identifier = "meme",
                    Data = null,
                    OriginName = config.ProcessName,
                    TargetName = "Meta1",
                    Status = MessageStatus.DataRequest,
                });

            Console.Read();
        }
    }
}
