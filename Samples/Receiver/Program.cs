﻿using Octovisor.Client;
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
            OctoConfig config = new OctoConfig
            {
                Token = "cool",
                Address = "localhost",
                Port = 6558,
                ProcessName = "Process2",
            };

            OctoClient client = new OctoClient(config);
            client.Log += log => Console.WriteLine($"{log.Severity} >> {log.Content}");

            await client.ConnectAsync();
            Console.WriteLine("Ready");

            foreach (RemoteProcess proc in client.AvailableProcesses)
                Console.WriteLine(proc.Name);

            client.OnTransmission<TestClass, string>("meme", (proc, data) =>
            {
                Console.WriteLine($"{proc.Name}: {(data is null ? "null" : data.ToString())}");

                return "hello world";
            });

            await Task.Delay(-1);
        }
    }
}
