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
                ServerAddress = Dns.GetHostName(),
                ServerPort = 1100,
            };

            OctovisorClient client = new OctovisorClient(config);
            client.OnError += e => Console.WriteLine(e);

            RemoteProcess process = client.ListenToProcess("ProcessA");
            MessageListener<int> listener = process.ListenToMessage<int>("TEST");

            int result = listener.Read();
            Console.WriteLine(result);
            Console.Read();
        }
    }
}
