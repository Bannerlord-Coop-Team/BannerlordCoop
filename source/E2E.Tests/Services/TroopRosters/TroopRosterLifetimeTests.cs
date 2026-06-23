using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters
{
    public class TroopRosterLifetimeTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public TroopRosterLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateTroopRoster_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? RosterId = null;
            server.Call(() =>
            {
                TroopRoster troopRoster = GameObjectCreator.CreateInitializedObject<TroopRoster>();
                Assert.True(server.ObjectManager.TryGetId(troopRoster, out RosterId));
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject<TroopRoster>(RosterId, out var _));

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(RosterId, out var _));
            }
        }

        [Fact]
        public void ClientCreateTroopRoster_DoesNothing()
        {
            // Arrange
            var server = TestEnvironment.Server;
            var client1 = TestEnvironment.Clients.First();
            // Act
            string? TroopRosterId = null;
            client1.Call(() =>
            {
                TroopRoster roster = GameObjectCreator.CreateInitializedObject<TroopRoster>();
                Assert.False(client1.ObjectManager.TryGetId(roster, out TroopRosterId));
            });

            // Assert
            Assert.False(server.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var _));
            foreach (var client in TestEnvironment.Clients)
            {
                Assert.False(client.ObjectManager.TryGetObject<TroopRoster>(TroopRosterId, out var _));
            }
        }
    }
}
