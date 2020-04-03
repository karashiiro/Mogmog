using Mogmog.Protos;
using NUnit.Framework;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;

namespace Mogmog.FFXIV
{
    [TestFixture]
    public class MogmogInteropConnectionManagerTests
    {
        private HttpClient http;
        private MogmogConfiguration config;
        private MogmogInteropConnectionManager connectionManager;

        [SetUp]
        public void SetUp()
        {
            config = new MogmogConfiguration();
            http = new HttpClient();
            connectionManager = new MogmogInteropConnectionManager(config, http);
        }

        [TearDown]
        public void TearDown()
        {
            connectionManager.Dispose();
            http.Dispose();
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
