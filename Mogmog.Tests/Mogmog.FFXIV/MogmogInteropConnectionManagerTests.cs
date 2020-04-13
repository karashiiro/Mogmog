using Mogmog.Logging;
using Mogmog.Protos;
using Mogmog.Tests.Stubs;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace Mogmog.FFXIV
{
    [TestFixture]
    public class MogmogInteropConnectionManagerTests
    {
        private HttpClient http;
        private MogmogInteropConnectionManager connectionManager;
        private TestLogger logger;

        private static readonly object[] callbackTestArgs =
        {
            new ChatMessageInterop
            {
                Message = new ChatMessage(),
                ChannelId = 2,
            },
            new ChatMessageInterop
            {
                Message = new ChatMessage
                {
                    Author = "Dummy Author",
                    Content = "dummy text",
                },
                ChannelId = 2,
            },
            "This is a string.",
            new GenericInterop
            {
                Command = "Error",
                Arg = "true",
            },
            new GenericInterop
            {
                Command = "NotError",
                Arg = "false",
            },
        };

        [SetUp]
        public void SetUp()
        {
            Mogger.Logger = logger = new TestLogger();

            http = new HttpClient();
            connectionManager = new MogmogInteropConnectionManager(new MogmogConfiguration(), http);
        }

        [TearDown]
        public void TearDown()
        {
            connectionManager.Dispose();
            http.Dispose();
        }

        [Test]
        public void Constructor_StartsProcessWithoutWindow()
        {
            Assert.AreEqual(IntPtr.Zero, connectionManager.GetMainWindowHandle());
        }

        [Test]
        public void AddHost_DoesNotThrowExceptions()
        {
            connectionManager.AddHost("https://localhost:5001");
            Assert.Pass();
        }

        [Test]
        public void RemoveHost_DoesNotThrowExceptions()
        {
            connectionManager.RemoveHost("https://localhost:5001");
            Assert.Pass();
        }

        [Test]
        public void MessageSend_DoesNotThrowExceptions()
        {
            var message = new ChatMessage();
            connectionManager.MessageSend(message, 0);
            Assert.Pass();
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [Parallelizable]
        public void MessageReceived_CallsMessageReceivedEventAppropriately(int idx)
        {
            bool messageIsChatMessageInterop = callbackTestArgs[idx] is ChatMessageInterop;
            bool messageReceivedCalled = false;
            connectionManager.MessageReceivedEvent += (sender, e) =>
            {
                messageReceivedCalled = true;
            };
            var messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(callbackTestArgs[idx]));
            using var messageStream = new MemoryStream(messageBytes);
            connectionManager.UpgradeLayerMessageReceived(messageStream);
            Assert.IsTrue(messageReceivedCalled && messageIsChatMessageInterop || !messageReceivedCalled && !messageIsChatMessageInterop, "Expected both true or both false, got {0} and {1}.", messageReceivedCalled, messageIsChatMessageInterop);
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void MessageReceived_CallsLogEventAppropriately(int idx)
        {
            bool messageIsGenericInterop = callbackTestArgs[idx] is GenericInterop;
            var messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(callbackTestArgs[idx]));
            using var messageStream = new MemoryStream(messageBytes);
            connectionManager.UpgradeLayerMessageReceived(messageStream);
            Assert.IsTrue(logger.LogCalledTimes == 1 && messageIsGenericInterop || logger.LogCalledTimes == 0 && !messageIsGenericInterop, "Expected both true or both false, got {0} and {1}.", logger.LogCalledTimes == 1, messageIsGenericInterop);
        }
    }

    [TestFixture]
    public class MogmogInteropConnectionManagerDisposeTest
    {
        [Test]
        public void Dispose_ClosesUpgradeLayer()
        {
            var config = new MogmogConfiguration();
            var http = new HttpClient();
            var connectionManager = new MogmogInteropConnectionManager(config, http);
            connectionManager.Dispose();
            var processes = Process.GetProcessesByName("Mogmog.FFXIV.UpgradeLayer");
            Assert.IsTrue(processes.Length == 0, "Expected empty array, got {0}", string.Join(",", processes.Select((process) => process.ProcessName)));
        }
    }
}
