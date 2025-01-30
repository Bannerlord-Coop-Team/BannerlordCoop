using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventParties
{
    public class MapEventPartySyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private string MapEventPartyId;
        private string PartyId;
        private int newInt = 50;
        private float newFloat = 25f;

        public MapEventPartySyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
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

            // Act
            server.Call(() =>
            {
                var mapEventParty = GameObjectCreator.CreateInitializedObject<MapEventParty>();
                var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
                var party = new PartyBase(mobileParty);

                // Create objects on the server
                Assert.True(server.ObjectManager.TryGetId(mapEventParty, out MapEventPartyId));
                Assert.True(server.ObjectManager.TryGetId(party, out PartyId));

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

            }
        }
    }
}
