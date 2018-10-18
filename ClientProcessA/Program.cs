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
            OctovisorConfig config = new OctovisorConfig
            {
                ProcessName = "ProcessA",
                ServerAddress = Dns.GetHostName(),
                ServerPort = 1100,
            };

            OctovisorClient client = new OctovisorClient(config);
            RemoteProcess process = client.ListenToProcess("ProcessB");
            MessageListener<int> listener = process.ListenToMessage<int>("TEST");

            listener.Write(1);

            Console.Read();
        }
    }
}
