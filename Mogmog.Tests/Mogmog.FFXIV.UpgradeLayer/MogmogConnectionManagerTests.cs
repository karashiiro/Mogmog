using NUnit.Framework;

namespace Mogmog.FFXIV.UpgradeLayer
{
    [TestFixture]
    [Ignore("Need to figure out how to test these methods.")]
    public class MogmogConnectionManagerTests
    {
        private MogmogConfiguration config;
        private MogmogConnectionManager connectionManager;

        [SetUp]
        public void Setup()
        {
            config = new MogmogConfiguration();
            connectionManager = new MogmogConnectionManager(config);
        }

        [TearDown]
        public void Teardown()
        {
            connectionManager.Dispose();
        }

        [Test]
        public void AddHost_IsInSyncWithConfig()
        {
            string testHost = "http://localhost:5001";
            connectionManager.AddHost(testHost, false);
            Assert.AreEqual(config.Hosts[0].Hostname, testHost);
            Assert.IsTrue(config.Hosts.Count == 1);
        }

        [Test]
        public void RemoveHost_IsInSyncWithConfig()
        {
            connectionManager.AddHost("http://localhost:5001", false);
            connectionManager.RemoveHost("http://localhost:5001");
            Assert.IsTrue(config.Hosts.Count == 0);
        }

        [Test]
        public void RemoveHost_DoesNotChangeIfEmpty()
        {
            connectionManager.RemoveHost("http://localhost:5001");
            Assert.IsTrue(config.Hosts.Count == 0);
        }
    }
}
