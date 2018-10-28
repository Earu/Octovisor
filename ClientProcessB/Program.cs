using Octovisor.Client;
using Octovisor.Client.Models;
using System;
using System.Net;

namespace Octovisor.Tests.ClientProcessB
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "ProcessB";

            OctovisorConfig config = new OctovisorConfig
            {
                ProcessName = "ProcessB",
                ServerAddress = "127.0.0.1",
                ServerPort = 1100,
            };

            OctovisorClient client = new OctovisorClient(config);
            client.OnError += e => Console.WriteLine(e);
            client.Log += Console.WriteLine;

            client.SendGarbage("ProcessA", "Faggot");
            Console.Read();
        }
    }
}
