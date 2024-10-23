using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit.Abstractions;

namespace E2E.Tests.Services.ItemRosters
{
    public class ItemRosterLifetimeTests
    {
        E2ETestEnvironment TestEnvironment { get; }

        private readonly string RosterId;

        public ItemRosterLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateItemRoster_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? itemRosterId = null;
            server.Call(() =>
            {
                ItemRoster itemRoster = new();

                Assert.True(server.ObjectManager.TryGetId(itemRoster, out itemRosterId));
            });

            // Assert
            Assert.NotNull(itemRosterId);

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<ItemRoster>(itemRosterId, out var _));
            }
        }

        [Fact]
        public void ClientCreateBuilding_DoesNothing()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            // Act
            string? rosterId = null;
            client1.Call(() =>
            {
                ItemRoster itemRoster = new();

                Assert.False(client1.ObjectManager.TryGetId(itemRoster, out rosterId));
            });

            // Assert
            Assert.Null(rosterId);
        }
    }
}
