using NUnit.Framework;

namespace Mogmog.FFXIV.UpgradeLayer
{
    [TestFixture]
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
            string testHost = "https://localhost:5001";
            connectionManager.AddHost(testHost);
            Assert.AreEqual(config.Hostnames[0], testHost);
        }

        [Test]
        public void RemoveHost_IsInSyncWithConfig()
        {
            connectionManager.AddHost("https://localhost:5001");
            connectionManager.RemoveHost("https://localhost:5001");
            Assert.IsTrue(config.Hostnames.Count == 0);
        }

        [Test]
        public void RemoveHost_DoesNotThrowIfNotFound()
        {
            connectionManager.RemoveHost("https://localhost:5001");
            Assert.Pass();
        }
    }
}
