using Mogmog.Protos;
using Mogmog.Server.Services;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Mogmog.Server
{
    [TestFixture]
    public class MogmogTransmissionServiceTests
    {
        private MogmogTransmissionService _transmitter;

        [SetUp]
        public void Setup()
        {
            _transmitter = new MogmogTransmissionService();
        }

        [Test]
        public void Send_EnqueuesDequeues()
        {
            var testMessage = new ChatMessage
            {
                Content = "whatever",
            };
            var eventFired = false;
            ChatMessage outMessage = new ChatMessage();
            var waiter = Task.Run(async () =>
            {
                while (!eventFired)
                    await Task.Delay(1);
            });
            _transmitter.MessageSent += (sender, e) =>
            {
                eventFired = true;
                outMessage = e.Message;
            };
            _transmitter.Send(testMessage);
            waiter.Wait();
            Assert.AreEqual(testMessage, outMessage);
        }
    }
}
