using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
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
            var firstClient = TestEnvironment.Clients.First();
            // Act
            string? TroopRosterId = null;
            firstClient.Call(() =>
            {
                var roster = GameObjectCreator.CreateInitializedObject<TroopRoster>();
                Assert.False(firstClient.ObjectManager.TryGetId(roster, out TroopRosterId));
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
