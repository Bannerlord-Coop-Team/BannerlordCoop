using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters
{
    public class TroopRosterSyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public TroopRosterSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerSyncTroopRoster_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? RosterId = null;
            string? PartyId = null;

            var elementVersionField = AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._troopRosterElementsVersion));
            var elementVersionIntercept = TestEnvironment.GetIntercept(elementVersionField);

            var countField = AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._count));
            var countIntercept = TestEnvironment.GetIntercept(countField);

            var isPrisonRosterField = AccessTools.Field(typeof(TroopRoster), nameof(TroopRoster._isPrisonRoster));
            var isPrisonRosterIntercept = TestEnvironment.GetIntercept(isPrisonRosterField);

            server.Call(() =>
            {
                TroopRoster troopRoster = GameObjectCreator.CreateInitializedObject<TroopRoster>();
                MobileParty ownerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
                CharacterObject character = GameObjectCreator.CreateInitializedObject<CharacterObject>();

                troopRoster.OwnerParty = ownerParty.Party;

                elementVersionIntercept.Invoke(null, new object[] { troopRoster, 42 });
                countIntercept.Invoke(null, new object[] { troopRoster, 69 });
                isPrisonRosterIntercept.Invoke(null, new object[] { troopRoster, true });

                Assert.True(server.ObjectManager.TryGetId(troopRoster, out RosterId));
                Assert.True(server.ObjectManager.TryGetId(ownerParty.Party, out PartyId));
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject<TroopRoster>(RosterId, out var _));

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(RosterId, out TroopRoster clientRoster));

                Assert.True(client.ObjectManager.TryGetId(clientRoster.OwnerParty, out string foundPartyId));

                Assert.Equal(PartyId, foundPartyId);
                Assert.Equal(69, clientRoster._count);
                Assert.Equal(42, clientRoster._troopRosterElementsVersion);
                Assert.True(clientRoster._isPrisonRoster);
            }
        }
    }
}
