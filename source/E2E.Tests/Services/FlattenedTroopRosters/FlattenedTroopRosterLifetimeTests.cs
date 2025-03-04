using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.FlattenedTroopRosters
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
        public void ServerCreateFlattenedTroopRoster_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? RosterId = null;
            server.Call(() =>
            {
                FlattenedTroopRoster flattenedTroopRoster = new FlattenedTroopRoster();
                Assert.True(server.ObjectManager.TryGetId(flattenedTroopRoster, out RosterId));
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject<FlattenedTroopRoster>(RosterId, out var _));

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<FlattenedTroopRoster>(RosterId, out var _));
            }
        }

        [Fact]
        public void ClientCreateFlattenedTroopRoster_DoesNothing()
        {
            // Arrange
            var server = TestEnvironment.Server;
            var client1 = TestEnvironment.Clients.First();
            // Act
            string? TroopRosterId = null;
            client1.Call(() =>
            {
                var Roster = new FlattenedTroopRoster();
                Assert.False(client1.ObjectManager.TryGetId(Roster, out TroopRosterId));
            });

            // Assert
            Assert.False(server.ObjectManager.TryGetObject<FlattenedTroopRoster>(TroopRosterId, out var _));
            foreach (var client in TestEnvironment.Clients)
            {
                Assert.False(client.ObjectManager.TryGetObject<FlattenedTroopRoster>(TroopRosterId, out var _));
            }
        }
    }
}
