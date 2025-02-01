using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Buildings
{
    public class BuildingLifetimeTests
    {
        E2ETestEnvironment TestEnvironment { get; }

        private readonly string TownId;

        public BuildingLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            var town = new Town();

            // Create fief on the server
            Assert.True(TestEnvironment.Server.ObjectManager.AddNewObject(town, out TownId));

            // Create fief on all clients
            foreach (var client in TestEnvironment.Clients)
            {
                var client_fief = new Town();
                Assert.True(client.ObjectManager.AddExisting(TownId, client_fief));
            }
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

            // Act
            string? buildingId = null;
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<Town>(TownId, out var town));

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

            // Act
            string? buildingId = null;
            client1.Call(() =>
            {
                Assert.True(client1.ObjectManager.TryGetObject<Town>(TownId, out var town));

                var building = new Building(BuildingType.All.First(), town, 5f, 1);

                Assert.False(client1.ObjectManager.TryGetId(building, out buildingId));
            });

            // Assert
            Assert.Null(buildingId);
        }
    }
}
