﻿using Grpc.Core;
using Mogmog.Protos;
using Mogmog.Server.Services;
using System.Collections.Generic;
using System.Linq;

namespace Mogmog.Tests
{
    public static class TestUtils
    {
        public static Grpc.Core.Server StartChatServer(int port)
        {
            var gds = new GameDataService();
            var transmitter = new MogmogTransmissionService();
            Grpc.Core.Server server = new Grpc.Core.Server
            {
                Services = { ChatService.BindService(new MogmogConnectionService(gds, transmitter)) },
                Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
            };
            server.Start();
            return server;
        }

        public static void StopChatServer(Grpc.Core.Server server)
            => server.ShutdownAsync().Wait();

        public static bool ElementsEqual<T>(this IList<T> me, IList<T> test) where T : class
        {
            if (me.Count() != test.Count())
                return false;
            for (var i = 0; i < me.Count(); i++)
            {
                if (me[i] != test[i])
                    return false;
            }
            return true;
        }
    }
}
