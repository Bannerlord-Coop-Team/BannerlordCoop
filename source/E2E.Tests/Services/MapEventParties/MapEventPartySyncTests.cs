using System.Reflection;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventParties
{
    public class MapEventPartySyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        private readonly List<MethodBase> disabledMethods;

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private string MapEventPartyId;
        private string MobilePartyId;
        private string PartyId;
        private int newInt = 50;
        private float newFloat = 25f;
        private TroopRoster newTroopRoster = new TroopRoster();
        private FlattenedTroopRoster newFlattened = new FlattenedTroopRoster();

        public MapEventPartySyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            disabledMethods = new List<MethodBase> {
                AccessTools.Method(typeof(MapEventParty), nameof(MapEventParty.Update))
            };

            MapEventPartyId = TestEnvironment.CreateRegisteredObject<MapEventParty>(disabledMethods);
            MobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_MapEventParty_Sync()
        {

            // Arrange
            var server = TestEnvironment.Server;

            var contributionToBattleField = AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._contributionToBattle));
            var diedInBattleField = AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._diedInBattle));
            var healthyManCountAtStartField = AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._healthyManCountAtStart));
            var rosterField = AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._roster));
            var woundedInBattleField = AccessTools.Field(typeof(MapEventParty), nameof(MapEventParty._woundedInBattle));

            var contributionToBattleIntercept = TestEnvironment.GetIntercept(contributionToBattleField);
            var diedInBattleIntercept = TestEnvironment.GetIntercept(diedInBattleField);
            var healthyManIntercept = TestEnvironment.GetIntercept(healthyManCountAtStartField);
            var rosterIntercept = TestEnvironment.GetIntercept(rosterField);
            var woundedInBattleIntercept = TestEnvironment.GetIntercept(woundedInBattleField);

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject(MobilePartyId, out MobileParty mobileParty));
                var party = new PartyBase(mobileParty);

                Assert.True(server.ObjectManager.TryGetObject(MapEventPartyId, out MapEventParty mapEventParty));
                Assert.True(server.ObjectManager.TryGetId(party, out PartyId));

                contributionToBattleIntercept.Invoke(null, new object[] { mapEventParty, newInt });
                diedInBattleIntercept.Invoke(null, new object[] { mapEventParty, newTroopRoster });
                healthyManIntercept.Invoke(null, new object[] { mapEventParty, newInt });
                rosterIntercept.Invoke(null, new object[] { mapEventParty, newFlattened });
                woundedInBattleIntercept.Invoke(null, new object[] { mapEventParty, newTroopRoster });

                mapEventParty.GainedInfluence = newFloat;
                mapEventParty.GainedRenown = newFloat;
                mapEventParty.GoldLost = newInt;
                mapEventParty.MoraleChange = newFloat;
                mapEventParty.Party = party;
                mapEventParty.PlunderedGold = newInt;

                Assert.Equal(newFloat, mapEventParty.GainedInfluence);
                Assert.Equal(newFloat, mapEventParty.GainedRenown);
                Assert.Equal(newInt, mapEventParty.GoldLost);
                Assert.Equal(newFloat, mapEventParty.MoraleChange);
                Assert.Equal(party, mapEventParty.Party);
                Assert.Equal(newInt, mapEventParty.PlunderedGold);

                Assert.Equal(newInt, mapEventParty._contributionToBattle);
                Assert.Equal(newTroopRoster, mapEventParty._diedInBattle);
                Assert.Equal(newInt, mapEventParty._healthyManCountAtStart);
                Assert.Equal(newFlattened, mapEventParty._roster);
                Assert.Equal(newTroopRoster, mapEventParty._woundedInBattle);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEventParty>(MapEventPartyId, out var clientMapEventParty));
                Assert.True(client.ObjectManager.TryGetObject<PartyBase>(PartyId, out var clientParty));

                Assert.Equal(newFloat, clientMapEventParty.GainedInfluence);
                Assert.Equal(newFloat, clientMapEventParty.GainedRenown);
                Assert.Equal(newInt, clientMapEventParty.GoldLost);
                Assert.Equal(newFloat, clientMapEventParty.MoraleChange);
                Assert.Equal(clientParty, clientMapEventParty.Party);
                Assert.Equal(newInt, clientMapEventParty.PlunderedGold);

                Assert.Equal(newInt, clientMapEventParty._contributionToBattle);
                Assert.Equal(newTroopRoster, clientMapEventParty._diedInBattle);
                Assert.Equal(newInt, clientMapEventParty._healthyManCountAtStart);
                Assert.Equal(newFlattened, clientMapEventParty._roster);
                Assert.Equal(newTroopRoster, clientMapEventParty._woundedInBattle);
            }
        }
    }
}
