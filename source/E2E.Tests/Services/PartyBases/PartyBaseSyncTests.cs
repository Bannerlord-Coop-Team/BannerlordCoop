using Autofac.Features.OwnedInstances;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Runtime.InteropServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyBases;

public class PartyBaseSyncTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    EnvironmentInstance Server => TestEnvironment.Server;

    IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    private string PartyId;
    private string ItemRosterId;
    private string MapEventSideId;
    private string SettlementId;
    private string MemberRosterId;
    private string PrisonRosterId;
    private string HeroId;
    private string PartyBaseId;

    public PartyBaseSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        PartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        ItemRosterId = TestEnvironment.CreateRegisteredObject<ItemRoster>();
        MapEventSideId = TestEnvironment.CreateRegisteredObject<MapEventSide>();
        MemberRosterId = TestEnvironment.CreateRegisteredObject<TroopRoster>();
        PrisonRosterId = TestEnvironment.CreateRegisteredObject<TroopRoster>();
        HeroId = TestEnvironment.CreateRegisteredObject<Hero>();
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Server_MobileParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        var customOwner = AccessTools.Field(typeof(PartyBase), nameof(PartyBase._customOwner));
        var index = AccessTools.Field(typeof(PartyBase), nameof(PartyBase._index));
        var lastEatingTime = AccessTools.Field(typeof(PartyBase), nameof(PartyBase._lastEatingTime));
        var lastMenPerTierVerNo = AccessTools.Field(typeof(PartyBase), nameof(PartyBase._lastNumberOfMenPerTierVersionNo));
        var mapEventSideField = AccessTools.Field(typeof(PartyBase), nameof(PartyBase._mapEventSide));
        var numberMenHorseField = AccessTools.Field(typeof(PartyBase), nameof(PartyBase._numberOfMenWithHorse));
        var remainingFoodPercentageField = AccessTools.Field(typeof(PartyBase), nameof(PartyBase._remainingFoodPercentage));
        var lastNumberMenHorseVerionField = AccessTools.Field(typeof(PartyBase), nameof(PartyBase._lastNumberOfMenWithHorseVersionNo));

        // Get field intercept to use on the server to simulate the field changing
        var mapEventSideIntercept = TestEnvironment.GetIntercept(mapEventSideField);
        var numberMenHorseIntercept = TestEnvironment.GetIntercept(numberMenHorseField);
        var indexIntercept = TestEnvironment.GetIntercept(index);
        var lastEatingTimeIntercept = TestEnvironment.GetIntercept(lastEatingTime);
        var lastMenPerTierVerNoIntercept = TestEnvironment.GetIntercept(lastMenPerTierVerNo);
        var customOwnerIntercept = TestEnvironment.GetIntercept(customOwner);
        var remainingFoodPercentageIntercept = TestEnvironment.GetIntercept(remainingFoodPercentageField);
        var lastNumberMenHorseVersionIntercept = TestEnvironment.GetIntercept(lastNumberMenHorseVerionField);

        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<ItemRoster>(ItemRosterId, out var itemRoster));
            Assert.True(server.ObjectManager.TryGetObject<MapEventSide>(MapEventSideId, out var mapEventSide));
            Assert.True(server.ObjectManager.TryGetObject<Settlement>(SettlementId, out var settlement));
            Assert.True(server.ObjectManager.TryGetObject<TroopRoster>(MemberRosterId, out var memberRoster));
            Assert.True(server.ObjectManager.TryGetObject<TroopRoster>(PrisonRosterId, out var prisonRoster));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(HeroId, out var hero));

            var partyBase = new PartyBase(default(MobileParty));
            Assert.True(server.ObjectManager.TryGetId(partyBase, out PartyBaseId));

            // Simulate the field changing
            remainingFoodPercentageIntercept.Invoke(null, new object[] { partyBase, 5 });
            mapEventSideIntercept.Invoke(null, new object[] { partyBase, mapEventSide });
            numberMenHorseIntercept.Invoke(null, new object[] { partyBase, 5 });
            indexIntercept.Invoke(null, new object[] { partyBase, 5 });
            lastEatingTimeIntercept.Invoke(null, new object[] { partyBase, new CampaignTime(5) });
            lastMenPerTierVerNoIntercept.Invoke(null, new object[] { partyBase, 5 });
            customOwnerIntercept.Invoke(null, new object[] { partyBase, hero });
            lastNumberMenHorseVersionIntercept.Invoke(null, new object[] { partyBase, 3 });

            partyBase.MobileParty = party;
            partyBase.Settlement = settlement;
            partyBase.IsVisualDirty = true;
            partyBase.ItemRoster = itemRoster;
            partyBase.LevelMaskIsDirty = true;
            partyBase.MapEventSide = mapEventSide;
            partyBase.MemberRoster = memberRoster;
            partyBase.PrisonRoster = prisonRoster;
            partyBase.RandomValue = 5;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(PartyBaseId, out var clientPartyBase));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(SettlementId, out var clientSettlement));
            Assert.True(client.ObjectManager.TryGetObject<ItemRoster>(ItemRosterId, out var clientItemRoster));
            Assert.True(client.ObjectManager.TryGetObject<MapEventSide>(MapEventSideId, out var clientMapEventSide));
            Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(MemberRosterId, out var clientMemberRoster));
            Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(PrisonRosterId, out var clientPrisonRoster));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(HeroId, out var clientHero));


            Assert.Equal(clientParty, clientPartyBase.MobileParty);
            Assert.True(clientPartyBase.IsVisualDirty);
            Assert.True(clientPartyBase.LevelMaskIsDirty);
            Assert.Equal(clientItemRoster, clientPartyBase.ItemRoster);
            Assert.Equal(clientMapEventSide, clientPartyBase.MapEventSide);
            Assert.Equal(clientMemberRoster, clientPartyBase.MemberRoster);
            Assert.Equal(clientPrisonRoster, clientPartyBase.PrisonRoster);
            Assert.Equal(5, clientPartyBase.RandomValue);
            Assert.Equal(clientSettlement, clientPartyBase.Settlement);
            Assert.Equal(clientHero, clientPartyBase._customOwner);
            Assert.Equal(5, clientPartyBase._index);
            Assert.Equal(new CampaignTime(5), clientPartyBase._lastEatingTime);
            Assert.Equal(5, clientPartyBase._lastNumberOfMenPerTierVersionNo);
            Assert.Equal(3, clientPartyBase._lastNumberOfMenWithHorseVersionNo);
            Assert.Equal(clientMapEventSide, clientPartyBase._mapEventSide);
            Assert.Equal(5, clientPartyBase._numberOfMenWithHorse);
            Assert.Equal(5, clientPartyBase._remainingFoodPercentage);
        }
    }
}

