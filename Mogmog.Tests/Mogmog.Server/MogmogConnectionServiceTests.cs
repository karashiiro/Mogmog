using Grpc.Core;
using Grpc.Core.Testing;
using Grpc.Core.Utils;
using Mogmog.Protos;
using Mogmog.Server.Services;
using Mogmog.Tests;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mogmog.Server
{
    [TestFixture]
    public class MogmogConnectionServiceTests
    {
        private MogmogTransmissionService _transmitter;

        private MogmogConnectionService _connection;

        [SetUp]
        public void Setup()
        {
            var gds = new GameDataService();
            _transmitter = new MogmogTransmissionService();

            _connection = new MogmogConnectionService(gds, _transmitter);
        }

        [TearDown]
        public void Teardown()
        {
            _connection.Dispose();
            _transmitter.Dispose();
        }

        [Test]
        public void Chat_InputPassesToOutput()
        {
            var testMessage = new ChatMessage
            {
                Author = "Dummy Author",
                Content = "dummy text",
                AuthorId = 0,
                AuthorId2 = 0,
                Avatar = string.Empty,
                Id = 0,
                World = string.Empty,
                WorldId = (int)PseudoWorld.Discord,
                AdditionalFlags = 0,
            };
            var fakeServerCallContext = TestServerCallContext.Create("Chat", null, DateTime.UtcNow.AddHours(1), new Metadata(), CancellationToken.None, "127.0.0.1", null, null, (metadata) => TaskUtils.CompletedTask, () => new WriteOptions(), (writeOptions) => { });
            var fakeReader = new TestAsyncStreamReader<ChatMessage>(testMessage);
            var fakeWriter = new TestServerStreamWriter<ChatMessage>();
            Task.Run(() => _connection.Chat(fakeReader, fakeWriter, fakeServerCallContext));
            fakeWriter.ReturnOnWrite().Wait(); // Internally, the transmitter calls a callback event which writes to this writer.
            Assert.AreEqual(testMessage, fakeWriter.Current);
        }

        [Test]
        [TestCase(23, "Asura")]
        [TestCase(49, "Kujata")]
        [TestCase(74, "Coeurl")]
        [TestCase((int)PseudoWorld.Discord, "Discord")]
        [TestCase((int)PseudoWorld.LINE, "LINE")]
        public void Chat_WorldIsSet(int id, string expectedWorldName)
        {
            var testMessage = new ChatMessage
            {
                Author = "Dummy Author",
                Content = "dummy text",
                AuthorId = 0,
                AuthorId2 = 0,
                Avatar = string.Empty,
                Id = 0,
                World = string.Empty,
                WorldId = id,
                AdditionalFlags = 0,
            };
            var fakeServerCallContext = TestServerCallContext.Create("Chat", null, DateTime.UtcNow.AddHours(1), new Metadata(), CancellationToken.None, "127.0.0.1", null, null, (metadata) => TaskUtils.CompletedTask, () => new WriteOptions(), (writeOptions) => { });
            var fakeReader = new TestAsyncStreamReader<ChatMessage>(testMessage);
            var fakeWriter = new TestServerStreamWriter<ChatMessage>();
            _connection.Chat(fakeReader, fakeWriter, fakeServerCallContext);
            fakeWriter.ReturnOnWrite().Wait();
            Assert.AreEqual(testMessage.World, expectedWorldName);
        }
    }
}
