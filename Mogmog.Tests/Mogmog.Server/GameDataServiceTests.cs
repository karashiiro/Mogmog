using Mogmog.Server.Services;
using NUnit.Framework;

namespace Mogmog.Server
{
    [TestFixture]
    public class GameDataServiceTests
    {
        [Test]
        public void GameDataService_InitializesWithoutExceptions()
        {
            new GameDataService();
            Assert.Pass();
        }
    }
}
