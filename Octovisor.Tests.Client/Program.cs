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

            OctovisorClient client = new OctovisorClient(config);
            client.ExceptionThrown += e => Console.WriteLine(e);
            client.Log += log => Console.WriteLine(log);

            await client.Connect();

            // Test spam to test server
            for(int i = 0; i < 10; i++)
                await client.Send(new Message
                {
                    Identifier = "meme",
                    Data = "LOL",
                    OriginName = config.ProcessName,
                    TargetName = "Meta2",
                    Status = MessageStatus.DataRequest,
                });

            await Task.Delay(-1);
        }
    }
}
