using Common.Util;
using Coop.Core.Server.Services.Instances;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// #3129: a battle whose first simulation already ran but one side has no healthy troops left (Coop applies
/// mission casualties live) never simulates again, so vanilla Update never picks a winner and the lords in it
/// stand at 0 troops for good.
/// </summary>
public class StalledMapEventTests : MapEventTestBase
{
    private const int TroopCount = 5;

    public StalledMapEventTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void StalledBattle_WithEveryDefenderWounded_FinalizesWithAttackerVictory()
    {
        var context = CreateStalledMapEvent();

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            UpdateRepeatedly(mapEvent);

            Assert.Equal(BattleState.AttackerVictory, mapEvent.BattleState);
            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
        }, MapEventDisabledMethods);

        AssertReleasedEverywhere(context);
    }

    [Fact]
    public void StalledSiegeAssault_WithEveryDefenderWounded_FinalizesWithAttackerVictory()
    {
        var context = CreateStalledMapEvent(MapEvent.BattleTypes.Siege);

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            UpdateRepeatedly(mapEvent);

            Assert.Equal(BattleState.AttackerVictory, mapEvent.BattleState);
            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
        }, MapEventDisabledMethods);

        AssertReleasedEverywhere(context);
    }

    [Fact]
    public void BattleWithHealthyTroopsOnBothSides_IsLeftToItsSimulation()
    {
        var context = CreateStalledMapEvent(woundDefenders: false);

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            mapEvent._nextSimulationTime = CampaignTime.Never;

            Assert.Equal(0, Server.Resolve<IStalledMapEventResolver>().ResolveStalledMapEvents());
            UpdateRepeatedly(mapEvent);

            Assert.Equal(BattleState.None, mapEvent.BattleState);
            Assert.False(mapEvent.IsFinalized);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void StalledBattle_WhileItsMissionIsOccupied_WaitsUntilTheMissionEmpties()
    {
        var context = CreateStalledMapEvent();
        var peer = Clients.First().NetPeer;

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            Assert.True(ServerBattleModeArbiter.TryClaimMission(context.MapEventId));
            Assert.True(Server.Resolve<IMissionManager>().TryEnterMission(peer, "mission-player", context.MapEventId, out _));

            UpdateRepeatedly(mapEvent);
            Assert.Equal(0, Server.Resolve<IStalledMapEventResolver>().ResolveStalledMapEvents());
            Assert.False(mapEvent.IsFinalized);

            // The last member leaving releases the claim; nothing else would end the battle now.
            Assert.True(Server.Resolve<IMissionManager>().TryLeaveMission(peer, "mission-player", context.MapEventId, out _));
            Assert.True(ServerBattleModeArbiter.ReleaseMission(context.MapEventId));

            UpdateRepeatedly(mapEvent);
            Assert.Equal(BattleState.AttackerVictory, mapEvent.BattleState);
            Assert.True(mapEvent.IsFinalized);
        }, MapEventDisabledMethods);

        AssertReleasedEverywhere(context);
    }

    [Fact]
    public void StalledBattle_WhileAServerSimulationOwnsIt_IsNotResolved()
    {
        var context = CreateStalledMapEvent();

        try
        {
            Server.Call(() =>
            {
                var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
                Assert.True(ServerBattleModeArbiter.TryClaimSimulation(context.MapEventId));

                UpdateRepeatedly(mapEvent);
                Assert.Equal(0, Server.Resolve<IStalledMapEventResolver>().ResolveStalledMapEvents());

                Assert.Equal(BattleState.None, mapEvent.BattleState);
                Assert.False(mapEvent.IsFinalized);
            }, MapEventDisabledMethods);
        }
        finally
        {
            Server.Call(() => ServerBattleModeArbiter.Release(context.MapEventId));
        }
    }

    [Fact]
    public void StalledBattle_WithAConnectedPlayerInIt_IsLeftToThatPlayer()
    {
        var context = CreateStalledMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("player");
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer("player", Clients.First().NetPeer));

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            AddSyntheticMapEventParty(mapEvent.AttackerSide, Server.GetRegisteredObject<MobileParty>(playerPartyId).Party);

            UpdateRepeatedly(mapEvent);
            Assert.Equal(0, Server.Resolve<IStalledMapEventResolver>().ResolveStalledMapEvents());

            Assert.Equal(BattleState.None, mapEvent.BattleState);
            Assert.False(mapEvent.IsFinalized);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void StalledBattle_WithAnOfflinePlayer_ResolvesOnlyAfterTheGracePeriod()
    {
        var context = CreateStalledMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("offline-player");

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            AddSyntheticMapEventParty(mapEvent.AttackerSide, Server.GetRegisteredObject<MobileParty>(playerPartyId).Party);

            // The player may still rejoin and fight it.
            UpdateRepeatedly(mapEvent);
            Assert.Equal(0, Server.Resolve<IStalledMapEventResolver>().ResolveStalledMapEvents());
            Assert.False(mapEvent.IsFinalized);

            MapEventPatches.SetAllPlayersOfflineSince(mapEvent,
                System.DateTime.UtcNow - MapEventConfig.OfflinePlayerGracePeriod - System.TimeSpan.FromMinutes(1));
            UpdateRepeatedly(mapEvent);

            Assert.Equal(BattleState.AttackerVictory, mapEvent.BattleState);
            Assert.True(mapEvent.IsFinalized);
            Assert.Null(Server.GetRegisteredObject<MobileParty>(playerPartyId).Party.MapEventSide);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void StalledBattle_InALoadedSave_IsFinalizedOnLoad()
    {
        var context = CreateStalledMapEvent();

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);

            // Load runs before campaign time moves, so no Update ends it first.
            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Equal(BattleState.AttackerVictory, mapEvent.BattleState);
            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
        }, MapEventDisabledMethods);

        AssertReleasedEverywhere(context);
    }

    [Fact]
    public void StalledBattle_IsEndedByUnstuckWhileCampaignTimeIsStopped()
    {
        var context = CreateStalledMapEvent();
        CreatePlayerHeroParty("unstuck-player");
        string[] result = null;

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer("unstuck-player", out var player));
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);

            result = Server.Resolve<IServerPlayerUnstuck>().Apply(player, includeCaptivity: false);

            Assert.Equal(BattleState.AttackerVictory, mapEvent.BattleState);
        }, MapEventDisabledMethods);

        Assert.Contains("Ended 1 battles left with no healthy troops on one side.", result);
        AssertReleasedEverywhere(context);
    }

    [Fact]
    public void FinalizeThatThrowsAfterEndingTheEvent_StillReleasesItsParties()
    {
        // A listener throwing after vanilla set WaitingRemoval used to leave both parties linked to a battle
        // the map event manager then dropped, so nothing could end it again.
        var context = CreateServerMapEvent();
        var listenerOwner = new object();

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(listenerOwner,
                _ => throw new System.InvalidOperationException("map event ended listener failed"));
            try
            {
                mapEvent.FinalizeEvent();
            }
            finally
            {
                CampaignEvents.MapEventEnded.ClearListeners(listenerOwner);
            }

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
        }, MapEventDisabledMethods);

        AssertReleasedEverywhere(context, defenderDefeated: false);
    }

    /// <summary>
    /// A battle whose first update already ran, with healthy attackers and, unless told otherwise, every
    /// defender wounded the way a live mission leaves them.
    /// </summary>
    private MapEventContext CreateStalledMapEvent(MapEvent.BattleTypes? battleType = null, bool woundDefenders = true)
    {
        var context = CreateServerMapEvent();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var settlementId = battleType.HasValue ? TestEnvironment.CreateRegisteredObject<Settlement>() : null;

        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(context.MapEventId);
            var defender = Server.GetRegisteredObject<MobileParty>(context.DefenderPartyId);
            var attacker = Server.GetRegisteredObject<MobileParty>(context.AttackerPartyId);

            using (new AllowedThread())
            {
                if (woundDefenders)
                {
                    defender.MemberRoster.Clear();
                    defender.MemberRoster.AddToCounts(Server.GetRegisteredObject<CharacterObject>(troopId), TroopCount, woundedCount: TroopCount);
                }

                if (battleType.HasValue)
                {
                    mapEvent.SetBattleType(battleType.Value);
                    mapEvent.SetMapEventSettlement(Server.GetRegisteredObject<Settlement>(settlementId));
                }

                mapEvent.FirstUpdateIsDone = true;

                // Each test party has its own clan, and a battle between factions at peace ends on its own.
                // The test stance link only covers the declaring side, so declare it from both.
                FactionManager.DeclareWar(attacker.MapFaction, defender.MapFaction);
                FactionManager.DeclareWar(defender.MapFaction, attacker.MapFaction);
            }

            Assert.True(attacker.Party.NumberOfHealthyMembers > 0);
            Assert.Equal(woundDefenders, mapEvent.DefenderSide.RecalculateMemberCountOfSide() == 0);
            Assert.Equal(BattleState.None, mapEvent.BattleState);
        }, MapEventDisabledMethods);

        return context;
    }

    private static void UpdateRepeatedly(MapEvent mapEvent)
    {
        for (int i = 0; i < 50 && !mapEvent.IsFinalized; i++)
            mapEvent.Update();
    }

    /// <summary>
    /// The battle is gone and its winner free on every instance. A defeated side with no healthy troops gets
    /// vanilla's defeat handling, which destroys its party so the lord can raise a new one.
    /// </summary>
    private void AssertReleasedEverywhere(MapEventContext context, bool defenderDefeated = true)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out _));
                Assert.Null(instance.GetRegisteredObject<MobileParty>(context.AttackerPartyId).Party.MapEventSide);
                if (defenderDefeated)
                    Assert.False(instance.ObjectManager.TryGetObject<MobileParty>(context.DefenderPartyId, out _));
                else
                    Assert.Null(instance.GetRegisteredObject<MobileParty>(context.DefenderPartyId).Party.MapEventSide);
            });
        }
    }

    private static void AddSyntheticMapEventParty(MapEventSide side, PartyBase party)
    {
        party._mapEventSide = side;
        side._battleParties.Add(new MapEventParty(party));
    }
}
