using Common.Util;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Verifies which map-event participant types allow the server's native campaign simulation to advance.
/// </summary>
public class MapEventUpdateAuthorityTests : MapEventTestBase
{
    public MapEventUpdateAuthorityTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AiOnlyMapEvent_WithSettlementParty_ServerUpdateIsAllowed()
    {
        var context = CreateServerMapEvent();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            AddSyntheticMapEventParty(mapEvent.DefenderSide, settlement.Party);

            Assert.True(InvokeMapEventUpdatePrefix(mapEvent));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void MapEvent_WithPlayerMobileParty_ServerUpdateIsBlocked()
    {
        var context = CreateServerMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("player");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));

            AddSyntheticMapEventParty(mapEvent.AttackerSide, playerParty.Party);

            Assert.False(InvokeMapEventUpdatePrefix(mapEvent));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void MapEvent_WithMissingParty_ServerUpdateIsBlocked()
    {
        var context = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));

            mapEvent.DefenderSide._battleParties.Add(ObjectHelper.SkipConstructor<MapEventParty>());

            Assert.False(InvokeMapEventUpdatePrefix(mapEvent));
        }, MapEventDisabledMethods);
    }

    private static void AddSyntheticMapEventParty(MapEventSide side, PartyBase party)
    {
        party._mapEventSide = side;
        side._battleParties.Add(new MapEventParty(party));
    }

    private static bool InvokeMapEventUpdatePrefix(MapEvent mapEvent)
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.MapEvents.Patches.MapEventPatches");
        var prefix = AccessTools.Method(patchType, "PrefixUpdate");
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, new object[] { mapEvent })!;
    }
}
