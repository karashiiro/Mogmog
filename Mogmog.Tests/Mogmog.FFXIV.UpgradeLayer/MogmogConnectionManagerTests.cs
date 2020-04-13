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
            connectionManager.AddHost(testHost);
            Assert.AreEqual(config.Hostnames[0], testHost);
            Assert.IsTrue(config.Hostnames.Count == 1);
        }

        [Test]
        public void RemoveHost_IsInSyncWithConfig()
        {
            connectionManager.AddHost("http://localhost:5001");
            connectionManager.RemoveHost("http://localhost:5001");
            Assert.IsTrue(config.Hostnames.Count == 0);
        }

        [Test]
        public void RemoveHost_DoesNotChangeIfEmpty()
        {
            connectionManager.RemoveHost("http://localhost:5001");
            Assert.IsTrue(config.Hostnames.Count == 0);
        }
    }
}
