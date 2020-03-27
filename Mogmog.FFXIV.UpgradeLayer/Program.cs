﻿using Mogmog.Protos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mogmog.FFXIV.UpgradeLayer
{
    class Program
    {
        static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();

        static async Task MainAsync(string[] args)
        {
            //var config = JsonConvert.DeserializeObject<MogmogConfiguration>(args[0]);
            var config = new MogmogConfiguration
            {
                Hostnames = new List<string>(),
            };
            config.Hostnames.Add("https://localhost:5001");
            var connectionManager = new MogmogConnectionManager(config)
            {
                MessageReceivedDelegate = MessageReceived
            };
            await Task.Delay(-1);
        }

        static void MessageReceived(ChatMessage message, int channelId)
        {
            // Send the message back to the caller.
            Console.WriteLine($"({message.Author}) {message.Content}");
        }
    }
}
