using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements
{
    public class SettlementFieldTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        public SettlementFieldTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_Settlement_Fields()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            const float newFloat = 540;
            const int newInt = 5;
            Vec2 newVec2 = new Vec2(5f, 3f);
            TextObject newText = new TextObject("test");

            string settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
            string heroId = TestEnvironment.CreateRegisteredObject<Hero>();
            string cultureId = TestEnvironment.CreateRegisteredObject<CultureObject>();
            //string? cultureId = null;
            string militiaComponentId = TestEnvironment.CreateRegisteredObject<MilitiaPartyComponent>();
            string stashId = TestEnvironment.CreateRegisteredObject<ItemRoster>();
            string townId = TestEnvironment.CreateRegisteredObject<Town>();
            string villageId = TestEnvironment.CreateRegisteredObject<Village>();
            string attackerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

            var canBeClaimedIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.CanBeClaimed)));
            var claimValueIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimValue)));
            var claimedByIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimedBy)));
            var hasVisitedIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.HasVisited)));
            var cultureIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.Culture)));
            var lastVisitTimeIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.LastVisitTimeOfOwner)));
            var militiaPartyComponentIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.MilitiaPartyComponent)));
            var numberLordsTargetingIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.NumberOfLordPartiesTargeting)));
            var stashIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.Stash)));
            var townIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.Town)));
            var villageIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.Village)));
            var gatePositionIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement._gatePosition)));
            var isVisibleIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement._isVisible)));
            var attackerPartyIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement._lastAttackerParty)));
            var locatorNodeIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement._locatorNodeIndex)));
            var nameIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement._name)));
            var nextLocatableIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement._nextLocatable)));
            var lordPartiesAtIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement._numberOfLordPartiesAt)));
            var positionIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement._position)));
            var readyMilitiaIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement._readyMilitia)));


            server.Call(() =>
            {
                //CultureObject cultureObject = new CultureObject();
                //cultureId = cultureObject.StringId;

                Assert.True(server.ObjectManager.TryGetObject<Settlement>(settlementId, out var serverSettlement));
                Assert.True(server.ObjectManager.TryGetObject<Hero>(heroId, out var serverHero));
                Assert.True(server.ObjectManager.TryGetObject<CultureObject>(cultureId, out var serverCulture));
                Assert.True(server.ObjectManager.TryGetObject<PartyComponent>(militiaComponentId, out var serverMilitiaComponent));
                Assert.True(server.ObjectManager.TryGetObject<ItemRoster>(stashId, out var serverStash));
                Assert.True(server.ObjectManager.TryGetObject<Town>(townId, out var serverTown));
                Assert.True(server.ObjectManager.TryGetObject<Village>(villageId, out var serverVillage));
                Assert.True(server.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var serverAttackerParty));

                canBeClaimedIntercept.Invoke(null, new object[] { serverSettlement, newInt });
                claimValueIntercept.Invoke(null, new object[] { serverSettlement, newFloat });
                claimedByIntercept.Invoke(null, new object[] { serverSettlement, serverHero });
                hasVisitedIntercept.Invoke(null, new object[] { serverSettlement, true });
                cultureIntercept.Invoke(null, new object[] { serverSettlement, serverCulture });
                lastVisitTimeIntercept.Invoke(null, new object[] { serverSettlement, newFloat });
                militiaPartyComponentIntercept.Invoke(null, new object[] { serverSettlement, serverMilitiaComponent });
                numberLordsTargetingIntercept.Invoke(null, new object[] { serverSettlement, newInt });
                stashIntercept.Invoke(null, new object[] { serverSettlement, serverStash });
                townIntercept.Invoke(null, new object[] { serverSettlement, serverTown });
                villageIntercept.Invoke(null, new object[] { serverSettlement, serverVillage });
                gatePositionIntercept.Invoke(null, new object[] { serverSettlement, newVec2});
                isVisibleIntercept.Invoke(null, new object[] { serverSettlement, true });
                attackerPartyIntercept.Invoke(null, new object[] { serverSettlement, serverAttackerParty });
                locatorNodeIntercept.Invoke(null, new object[] { serverSettlement, newInt });
                nameIntercept.Invoke(null, new object[] { serverSettlement, newText });
                nextLocatableIntercept.Invoke(null, new object[] { serverSettlement, serverSettlement });
                lordPartiesAtIntercept.Invoke(null, new object[] { serverSettlement, newInt });
                positionIntercept.Invoke(null, new object[] { serverSettlement, newVec2 });
                readyMilitiaIntercept.Invoke(null, new object[] { serverSettlement, newFloat });

                Assert.Equal(newInt, serverSettlement.CanBeClaimed);
                Assert.Equal(newFloat, serverSettlement.ClaimValue);
                Assert.Equal(serverHero, serverSettlement.ClaimedBy);
                Assert.True(serverSettlement.HasVisited);
                Assert.Equal(serverCulture, serverSettlement.Culture);
                Assert.Equal(newFloat, serverSettlement.LastVisitTimeOfOwner);
                Assert.Equal(serverMilitiaComponent, serverSettlement.MilitiaPartyComponent);
                Assert.Equal(newInt, serverSettlement.NumberOfLordPartiesTargeting);
                Assert.Equal(serverStash, serverSettlement.Stash);
                Assert.Equal(serverTown, serverSettlement.Town);
                Assert.Equal(serverVillage, serverSettlement.Village);
                Assert.Equal(newVec2, serverSettlement._gatePosition);
                Assert.True(serverSettlement._isVisible);
                Assert.Equal(serverAttackerParty, serverSettlement._lastAttackerParty);
                Assert.Equal(newInt, serverSettlement._locatorNodeIndex);
                Assert.Equal(newText, serverSettlement._name);
                Assert.Equal(serverSettlement, serverSettlement._nextLocatable);
                Assert.Equal(newInt, serverSettlement._numberOfLordPartiesAt);
                Assert.Equal(newVec2, serverSettlement._position);
                Assert.Equal(newFloat, serverSettlement._readyMilitia);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var clientSettlement));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var clientHero));
                Assert.True(client.ObjectManager.TryGetObject<CultureObject>(cultureId, out var clientCulture));
                Assert.True(client.ObjectManager.TryGetObject<PartyComponent>(militiaComponentId, out var clientMilitiaComponent));
                Assert.True(client.ObjectManager.TryGetObject<ItemRoster>(stashId, out var clientStash));
                Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientTown));
                Assert.True(client.ObjectManager.TryGetObject<Village>(villageId, out var clientVillage));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var clientAttackerParty));

                Assert.Equal(newFloat, clientSettlement.ClaimValue);
                Assert.Equal(newInt, clientSettlement.CanBeClaimed);
                Assert.Equal(clientHero, clientSettlement.ClaimedBy);
                Assert.Equal(clientCulture, clientSettlement.Culture);
                Assert.Equal(newFloat, clientSettlement.LastVisitTimeOfOwner);
                Assert.Equal(clientMilitiaComponent, clientSettlement.MilitiaPartyComponent);
                Assert.Equal(newInt, clientSettlement.NumberOfLordPartiesTargeting);
                Assert.Equal(clientStash, clientSettlement.Stash);
                Assert.Equal(clientTown, clientSettlement.Town);
                Assert.Equal(clientVillage, clientSettlement.Village);
                Assert.Equal(newVec2, clientSettlement._gatePosition);
                Assert.True(clientSettlement._isVisible);
                Assert.Equal(clientAttackerParty, clientSettlement._lastAttackerParty);
                Assert.Equal(newInt, clientSettlement._locatorNodeIndex);
                Assert.Equal(newText.ToString(), clientSettlement._name.ToString());
                Assert.Equal(clientSettlement, clientSettlement._nextLocatable);
                Assert.Equal(newInt, clientSettlement._numberOfLordPartiesAt);
                Assert.Equal(newVec2, clientSettlement._position);
                Assert.Equal(newFloat, clientSettlement._readyMilitia);
            }
        }
    }
}
