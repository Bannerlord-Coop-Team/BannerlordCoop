using E2E.Tests.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Localization;
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
            TestEnvironment.AssertProperty<SiegeStrategy, TextObject>(nameof(SiegeStrategy.Name), new TextObject("testStrategy"));
            TestEnvironment.AssertProperty<SiegeStrategy, TextObject>(nameof(SiegeStrategy.Description), new TextObject("testStrategy_description"));
        }
    }
}
