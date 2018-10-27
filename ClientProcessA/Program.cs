using Octovisor.Client;
using Octovisor.Client.Models;
using System;
using System.Net;

namespace Octovisor.Tests.ClientProcessA
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "ProcessA";

            OctovisorConfig config = new OctovisorConfig
            {
                ProcessName = "ProcessA",
                ServerAddress = Dns.GetHostName(),
                ServerPort = 1100,
            };

            OctovisorClient client = new OctovisorClient(config);
            client.OnError += e => Console.WriteLine(e); //debug

            Console.Read();
        }
    }
}
