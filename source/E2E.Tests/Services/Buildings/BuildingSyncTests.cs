using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Buildings
{
    public class BuildingSyncTests : SyncTestBase
    {
        private string buildingId;

        public BuildingSyncTests(ITestOutputHelper output) : base(output)
        {
            buildingId = TestEnvironment.CreateRegisteredObject<Building>();
            TestEnvironment.CreateRegisteredObject<Town>();
        }

        [Fact]
        public void Server_Building_Fields()
        {
            TestEnvironment.AssertField<Building, float>(nameof(Building._hitpoints),12345, defaultValue: 100);
            TestEnvironment.AssertField<Building, int>(nameof(Building._currentLevel), 2);
            TestEnvironment.AssertField<Building, bool>(nameof(Building.IsCurrentlyDefault), true);
            TestEnvironment.AssertField<Building, float>(nameof(Building.BuildingProgress), 24);
        }

        [Fact]
        public void Server_Building_Properties()
        {
            // Arrange
            Assert.True(Server.ObjectManager.TryGetObject(buildingId, out Building building));
            building.Town = null;

            // Assert
            TestEnvironment.AssertReferenceProperty<Building, Town>(nameof(Building.Town));
        }
    }
}
