﻿using Octovisor.Client;
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
                ServerAddress = "3kv.in",
                ServerPort = 6558,
                ProcessName = "TestProcess",
            };

            OctovisorClient client = new OctovisorClient(config);
            client.OnError += e => Console.WriteLine(e);
            client.Log += log => Console.WriteLine(log);

            client.Connect().Wait();

            client.Send(new Message
            {
                Identifier = "meme",
                Data = null,
                OriginName = config.ProcessName,
                TargetName = "Meta1",
                Status = MessageStatus.OK,
            });

            Console.Read();
        }
    }
}
