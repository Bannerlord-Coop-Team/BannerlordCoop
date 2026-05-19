using E2E.Tests.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeStrategies
{
    public class SiegeStrategySyncTests : SyncTestBase
    {
        private string siegeStrategyId;

        public SiegeStrategySyncTests(ITestOutputHelper output) : base(output)
        {
            siegeStrategyId = TestEnvironment.CreateRegisteredObject<SiegeStrategy>();
        }
        [Fact]
        public void Server_SiegeStrategy_Properties()
        {
            // Arrange
            Assert.True(Server.ObjectManager.TryGetObject(siegeStrategyId, out SiegeStrategy siegeStrategy));

            // Assert
            TestEnvironment.AssertProperty<SiegeStrategy, string>(nameof(SiegeStrategy.Name), "testStrategy");
            TestEnvironment.AssertProperty<SiegeStrategy, string>(nameof(SiegeStrategy.Description), "testStrategy_description");
        }
    }
}
