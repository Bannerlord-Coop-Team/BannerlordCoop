using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Buildings
{
    public class BuildingSyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private readonly string BuildingId;
        private readonly string TownId;
        private int newInt = 50;
        private float newFloat = 25f;

        public BuildingSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            var building = GameObjectCreator.CreateInitializedObject<Building>();
            var town = GameObjectCreator.CreateInitializedObject<Town>();

            // Create objects on the server
            Assert.True(Server.ObjectManager.AddNewObject(building, out BuildingId));
            Assert.True(Server.ObjectManager.AddNewObject(town, out TownId));

            // Create objects on all clients
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.AddExisting(BuildingId, building));
                Assert.True(client.ObjectManager.AddExisting(TownId, town));
            }
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_Building_Sync()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var hitpointsField = AccessTools.Field(typeof(Building), nameof(Building._hitpoints));
            var currentLevelField = AccessTools.Field(typeof(Building), nameof(Building._currentLevel));
            var isCurrentlyDefaultField = AccessTools.Field(typeof(Building), nameof(Building.IsCurrentlyDefault));
            var buildingProgressField = AccessTools.Field(typeof(Building), nameof(Building.BuildingProgress));

            // Get field intercept to use on the server to simulate the field changing
            var hitpointsIntercept = TestEnvironment.GetIntercept(hitpointsField);
            var currentLevelIntercept = TestEnvironment.GetIntercept(currentLevelField);
            var isCurrentlyDefaultIntercept = TestEnvironment.GetIntercept(isCurrentlyDefaultField);
            var buildingProgressIntercept = TestEnvironment.GetIntercept(buildingProgressField);

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<Building>(BuildingId, out var serverBuilding));
                Assert.True(server.ObjectManager.TryGetObject<Town>(TownId, out var serverTown));

                // Simulate the field changing
                hitpointsIntercept.Invoke(null, new object[] { serverBuilding, newFloat });
                currentLevelIntercept.Invoke(null, new object[] { serverBuilding, newInt });
                isCurrentlyDefaultIntercept.Invoke(null, new object[] { serverBuilding, true });
                buildingProgressIntercept.Invoke(null, new object[] { serverBuilding, newFloat });

                serverBuilding.Town = serverTown;

                Assert.Equal(newFloat, serverBuilding._hitpoints);
                Assert.Equal(newInt, serverBuilding._currentLevel);
                Assert.True(serverBuilding.IsCurrentlyDefault);
                Assert.Equal(newFloat, serverBuilding.BuildingProgress);

                Assert.Equal(serverTown, serverBuilding.Town);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Building>(BuildingId, out var clientBuilding));
                Assert.True(client.ObjectManager.TryGetObject<Town>(TownId, out var clientTown));

                Assert.Equal(newFloat, clientBuilding._hitpoints);
                Assert.Equal(newInt, clientBuilding._currentLevel);
                Assert.True(clientBuilding.IsCurrentlyDefault);
                Assert.Equal(newFloat, clientBuilding.BuildingProgress);

                Assert.Equal(clientTown, clientBuilding.Town);
            }
        }
    }
}
