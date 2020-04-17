using Mogmog.OAuth2;
using Mogmog.Tests.Stubs;
using Moq;
using NUnit.Framework;

namespace Mogmog.FFXIV
{
    [TestFixture]
    public class MogmogPluginTests
    {
        private Mock<IChatCommandHandler> fakeCommandHandler;
        private Mock<IConnectionManager> fakeConnectionManager;

        private MogmogConfiguration config;
        private TestMogmogPlugin mogmog;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            fakeCommandHandler = new Mock<IChatCommandHandler>();
            fakeConnectionManager = new Mock<IConnectionManager>();
        }

        [SetUp]
        public void Setup()
        {
            config = new MogmogConfiguration();
            mogmog = new TestMogmogPlugin();
            mogmog.SetCommandHandler(fakeCommandHandler.Object);
            mogmog.SetConfig(config);
            mogmog.SetConnectionManager(fakeConnectionManager.Object);
        }

        [Test]
        public void AddHost()
        {
            var hostname = "https://localhost:5001";
            mogmog.AddHost("/mgaddhost", hostname);
            Assert.IsTrue(config.Hostnames.Contains(hostname));
        }

        [Test]
        [TestCase("1")]
        [TestCase("https://localhost:5001")]
        public void RemoveHost_RemovesIDOrName(string input)
        {
            var hostname = "https://localhost:5001";
            mogmog.AddHost("/mgaddhost", hostname);
            Assert.IsTrue(config.Hostnames.Contains(hostname));
            mogmog.RemoveHost("/mgmgremovehost", input);
            Assert.IsFalse(config.Hostnames.Contains(hostname));
        }

        [Test]
        public void ReloadHost_DoesNotRemove()
        {
            var hostname = "https://localhost:5001";
            mogmog.AddHost("/mgaddhost", hostname);
            mogmog.ReloadHost("/mgreload", hostname);
            Assert.IsTrue(config.Hostnames.Contains(hostname));
        }
    }
}
