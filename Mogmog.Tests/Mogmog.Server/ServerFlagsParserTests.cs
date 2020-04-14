using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Mogmog.Server
{
    [TestFixture]
    public class ServerFlagsParserTests
    {
        [Test]
        public void Parser_ParsesFlags([Range(0, 32)]int enumIdx)
        {
            var test = (ServerFlags)Enum.Parse(typeof(ServerFlags), Enum.GetNames(typeof(ServerFlags))[enumIdx]);
            var flags = ServerFlagsParser.ParseFromConfig(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { test.ToString(), "True" },
                })
                .Build());
            var hasFlag = flags.HasFlag(test);
            var hasNoOtherFlags = ((int)flags ^ (int)test) == 0;
            Assert.IsTrue(hasFlag && hasNoOtherFlags, "Expected True and True, got {0} and {1}", hasFlag, hasNoOtherFlags);
        }
    }
}
