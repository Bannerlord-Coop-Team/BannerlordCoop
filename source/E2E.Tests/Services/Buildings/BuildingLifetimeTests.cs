using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Buildings
{
    public class BuildingLifetimeTests
    {
        E2ETestEnvironment TestEnvironment { get; }
        public BuildingLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateBuilding_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            string townId = TestEnvironment.CreateRegisteredObject<Town>();

            // Act
            string? buildingId = null;
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject(townId, out Town town));

                var building = new Building(BuildingType.All.First(), town, 5f, 1);

                Assert.True(server.ObjectManager.TryGetId(building, out buildingId));
            });

            // Assert
            Assert.NotNull(buildingId);

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Building>(buildingId, out var _));
            }
        }

        [Fact]
        public void ClientCreateBuilding_DoesNothing()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            string townId = TestEnvironment.CreateRegisteredObject<Town>();

            // Act
            string? buildingId = null;
            client1.Call(() =>
            {
                Assert.True(client1.ObjectManager.TryGetObject(townId, out Town town));

                var building = new Building(BuildingType.All.First(), town, 5f, 1);

                Assert.True(client1.ObjectManager.TryGetId(building, out buildingId));
            });

            // Assert
            Assert.Null(buildingId);
        }
    }
}
