using Octovisor.Client;
using Octovisor.Models;
using System;

namespace Octovisor.Tests.Client
{
    class Program
    {
        static void Main()
        {
            Console.Title = "Octovisor Client";

            ClientConfig config = new ClientConfig
            {
                Token = "MetaCosntructIsCool",
                ServerAddress = "threekelv.in",
                ServerPort = 6558,
                ProcessName = "TestProcess",
            };

            OctovisorClient client = new OctovisorClient(config);
            client.OnError += e => Console.WriteLine(e);
            client.Log += log => Console.WriteLine(log);

            client.Connect().Wait();

            for (uint i = 0; i < 100; i++)
            {
                client.SendGarbage("TargetProcess", "Test").Wait();
            }

            Console.Read();
        }
    }
}
