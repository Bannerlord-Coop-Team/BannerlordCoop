using Common.Util;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
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
    public void MapEvent_WithConnectedPlayerMobileParty_ServerUpdateIsBlocked()
    {
        var context = CreateServerMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("player");
        ConnectPlayer("player");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));

            AddSyntheticMapEventParty(mapEvent.AttackerSide, playerParty.Party);

            Assert.False(InvokeMapEventUpdatePrefix(mapEvent));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void MissionAcceptedPlayerMapEvent_WithAllowedThread_ServerUpdateRemainsBlocked()
    {
        var context = CreateServerMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("player");
        ConnectPlayer("player");

        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));

                AddSyntheticMapEventParty(mapEvent.AttackerSide, playerParty.Party);
                Assert.True(ServerBattleModeArbiter.TryClaimMission(context.MapEventId));

                using (new AllowedThread())
                    Assert.False(InvokeMapEventUpdatePrefix(mapEvent));
            }, MapEventDisabledMethods);
        }
        finally
        {
            Server.Call(() => ServerBattleModeArbiter.Release(context.MapEventId));
        }
    }

    [Fact]
    public void MapEvent_WithOnlyOfflinePlayers_ServerUpdateIsAllowed()
    {
        // #3864: nothing else resolves the battle once its players are gone
        var context = CreateServerMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("offline-player");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));

            AddSyntheticMapEventParty(mapEvent.AttackerSide, playerParty.Party);

            Assert.True(InvokeMapEventUpdatePrefix(mapEvent));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void MapEvent_WithOneConnectedAndOneOfflinePlayer_ServerUpdateIsBlocked()
    {
        var context = CreateServerMapEvent();
        var (_, offlinePartyId) = CreatePlayerHeroParty("offline-player");
        var (_, onlinePartyId) = CreatePlayerHeroParty("online-player");
        ConnectPlayer("online-player");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(offlinePartyId, out var offlineParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(onlinePartyId, out var onlineParty));

            AddSyntheticMapEventParty(mapEvent.AttackerSide, offlineParty.Party);
            AddSyntheticMapEventParty(mapEvent.DefenderSide, onlineParty.Party);

            Assert.False(InvokeMapEventUpdatePrefix(mapEvent));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void MapEvent_WithOfflinePlayerWhileServerSimulationRuns_ServerUpdateIsBlocked()
    {
        // a simulation started before the disconnect finishes first
        var context = CreateServerMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("offline-player");

        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));

                AddSyntheticMapEventParty(mapEvent.AttackerSide, playerParty.Party);
                Assert.True(ServerBattleModeArbiter.TryClaimSimulation(context.MapEventId));

                Assert.False(InvokeMapEventUpdatePrefix(mapEvent));
            }, MapEventDisabledMethods);
        }
        finally
        {
            Server.Call(() => ServerBattleModeArbiter.Release(context.MapEventId));
        }
    }

    [Fact]
    public void MapEvent_WithOnlyOfflinePlayers_ResolvesWhenTheServerUpdates()
    {
        // #3864: the battle used to stay on the map with neither side losing troops
        var context = CreateServerMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("offline-player");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            playerParty.Party._mapEventSide = mapEvent.AttackerSide;
            mapEvent.AttackerSide._battleParties.Add(new MapEventParty(playerParty.Party));

            for (int i = 0; i < 500 && mapEvent.State != MapEventState.WaitingRemoval; i++)
                mapEvent.Update();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
        }, MapEventDisabledMethods);
    }
    private void ConnectPlayer(string controllerId)
    {
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(controllerId, Clients.First().NetPeer));
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

    [Fact]
    public void MapEvent_WithStaleLeaderAndLockedAllocations_ServerUpdateRepairsSimulationSetup()
    {
        var context = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            var side = mapEvent.AttackerSide;
            var removedLeader = side.LeaderParty;
            var expectedLeader = GameObjectCreator.CreateInitializedObject<MobileParty>().Party;
            AddSyntheticMapEventParty(side, expectedLeader);

            side._troopAllocationsLocked = true;
            side._battleParties.RemoveAt(0);
            removedLeader._mapEventSide = null;

            Assert.True(InvokeMapEventUpdatePrefix(mapEvent));
            Assert.Same(expectedLeader, side.LeaderParty);
            Assert.False(side._troopAllocationsLocked);
        }, MapEventDisabledMethods);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MapEvent_WithEmptySideAndStaleFactionlessLeader_FinalizesAndReleasesReplicas(bool emptyAttacker)
    {
        var context = CreateServerMapEvent();
        string remainingPartyId = emptyAttacker ? context.DefenderPartyId : context.AttackerPartyId;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            var emptySide = emptyAttacker ? mapEvent.AttackerSide : mapEvent.DefenderSide;
            var removedParty = emptySide.LeaderParty;
            emptySide._battleParties.Clear();
            removedParty._mapEventSide = null;
            emptySide.LeaderParty = ObjectHelper.SkipConstructor<PartyBase>();
            Assert.Null(emptySide.LeaderParty.MapFaction);

            mapEvent.Update();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Empty(mapEvent.InvolvedParties);
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(remainingPartyId, out var remainingParty));
            Assert.Null(remainingParty.MapEvent);
        }, MapEventDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(remainingPartyId, out var remainingParty));
            Assert.Null(remainingParty.MapEvent);
        }
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
