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
                Token = "MEMES",
                ServerAddress = "127.0.0.1",
                ServerPort = 1100,
                ProcessName = "TestProcess",
            };

            OctovisorClient client = new OctovisorClient(config);
            client.OnError += e => Console.WriteLine(e);
            client.Log += log => Console.WriteLine(log);

            while (!client.IsConnected) ;

            client.SendGarbage("TargetProcess", "Test");

        }
    }
}
