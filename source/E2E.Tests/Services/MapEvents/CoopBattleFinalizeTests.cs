using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// The "back up to the encounter" end of a coop battle. The close instruction never force-finishes a player's
/// <c>PlayerEncounter</c>: on a concluded battle each winner's encounter is detached and staged for the
/// battle-results pass, which the <c>PlayerEncounter.Update</c> path drives (loot states and the finish) in the
/// live game — force-finishing would skip the map event results screens. An encounter with no results to show
/// (the battle was left rather than concluded) is instead finished by the client's map-event-destroy fallback
/// (<c>MapEventRegistry.CloseDestroyedMapEventEncounterIfNeeded</c>) once the shared event is gone from under
/// its menu. Each client controls its own <see cref="MobileParty.MainParty"/>, so per-client encounter paths
/// run for real (see <see cref="MapEventTestBase.EnableHeadlessEncounterFinish"/>); only
/// <c>GameMenu.ExitToLast</c> (no menu context exists headless) is mocked where noted.
/// </summary>
public class CoopBattleFinalizeTests : MapEventTestBase
{
    public CoopBattleFinalizeTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void MissionStart_WoundedSoleOpponent_FinalizesWithoutOpeningMission()
    {
        var setup = SetupTwoOpposingPlayersInBattle();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(setup.recipientHeroId, out var recipientHero));
            recipientHero.HitPoints = 1;
            Assert.True(recipientHero.IsWounded);
        }, MapEventDisabledMethods);

        Server.NetworkSentMessages.Clear();

        try
        {
            var initiatorClient = Clients.First();
            initiatorClient.Call(() => initiatorClient.Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                setup.ctx.MapEventId,
                setup.initiatorPartyId)), MapEventDisabledMethods);

            var reply = Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>().Single();
            Assert.False(reply.Accepted);
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>());
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkStartSiegeMission>());
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>());

            AssertMapEventRemoved(Server, setup.ctx.MapEventId);
            foreach (var client in Clients)
                AssertMapEventRemoved(client, setup.ctx.MapEventId);

            Assert.True(ServerBattleModeArbiter.TryClaimSimulation(setup.ctx.MapEventId));
        }
        finally
        {
            ServerBattleModeArbiter.Release(setup.ctx.MapEventId);
        }
    }

    /// <summary>
    /// The explicit post-victory leave: the winning side leader finalizes, the server tears down the shared
    /// <see cref="MapEvent"/> and tells both players to close. The close instruction itself only detaches each
    /// player's MainParty from the battle — it never touches the local <c>PlayerEncounter</c>, whose unwind is
    /// owned by the <c>PlayerEncounter.Update</c> path so pending battle-result states can still run (see
    /// <c>PvPInteractionClientHandler.CloseEncounter</c> and <c>PlayerEncounterPatches.UpdatePrefix</c>). A
    /// leave like this stages no results, so each player's encounter — left open at a menu whose battle no
    /// longer exists — is finished by the client's map-event-destroy fallback
    /// (<c>MapEventRegistry.CloseDestroyedMapEventEncounterIfNeeded</c>).
    /// </summary>
    [Fact]
    public void BothPlayersLeave_FinalizesSharedEncounter_AndEachPlayerLeavesTheBattle()
    {
        var (ctx, _, player1PartyBaseId, player2PartyBaseId) = SetupTwoAlliedPlayersInBattle();

        // Each player is in its own local post-battle encounter (no attached map event, so Finish's FinalizeBattle
        // is a no-op).
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        // Capture the "close your encounter" instruction each player's client receives.
        string[] closeOnClient1 = null, closeOnClient2 = null;
        Clients.First().Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(p => closeOnClient1 = p.What.PartyIds);
        Clients.Last().Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(p => closeOnClient2 = p.What.PartyIds);

        // The winning side leader leaves the victory screen -> the coop finalize round-trip. PlayerEncounter.Finish
        // runs for real; only GameMenu.ExitToLast (no menu context exists headless) is mocked.
        var disabled = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList();

        var client1 = Clients.First();
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            client1.Resolve<IMessageBroker>().Publish(this, new MapEventFinalizeAttempted(mapEvent));
        }, disabled);

        // The shared battle was finalized authoritatively and removed from the server and BOTH players' clients.
        AssertMapEventRemoved(Server, ctx.MapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, ctx.MapEventId);

        // Both players' clients were told to close their encounter, and the instruction named both player parties.
        Assert.NotNull(closeOnClient1);
        Assert.NotNull(closeOnClient2);
        Assert.Contains(player1PartyBaseId, closeOnClient1);
        Assert.Contains(player2PartyBaseId, closeOnClient1);
        Assert.Contains(player1PartyBaseId, closeOnClient2);
        Assert.Contains(player2PartyBaseId, closeOnClient2);

        // Each player's client recognized its OWN MainParty in the close instruction and ran the close path,
        // detaching it from the finalized battle.
        AssertMainPartyLeftBattle(Clients.First());
        AssertMainPartyLeftBattle(Clients.Last());

        // Neither player has battle results staged (the battle was left, not concluded), so the map-event-destroy
        // fallback finished both players' now-menuless encounters when the shared event was torn down. A concluded
        // battle instead keeps each winner's encounter open for the battle-results pass — see
        // BattleConcludesWithVictory_StagesEachWinnersEncounterForBattleResults.
        AssertHasPlayerEncounter(Clients.First(), expected: false);
        AssertHasPlayerEncounter(Clients.Last(), expected: false);
    }

    [Fact]
    public void ActiveMission_DestroyBeforeClose_PreservesMapEventUntilMissionExit()
    {
        var (ctx, _, _, successorPartyBaseId) = SetupTwoAlliedPlayersInBattle();
        var successor = Clients.Last();
        MapEvent destroyedMapEvent = null;
        MockMission mission = null;

        using (var fixture = new MissionEngineFixture())
        {
            successor.Call(() =>
            {
                mission = fixture.CreateMission(successor);
                Assert.True(successor.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out destroyedMapEvent));
            });

            successor.SimulateMessage(Server, new NetworkDestroyInstance<MapEvent>(ctx.MapEventId));
            successor.SimulateMessage(Server, new NetworkClosePvpEncounter(
                new[] { successorPartyBaseId }, mapEventId: ctx.MapEventId));

            AssertMapEventRemoved(successor, ctx.MapEventId);
            successor.Call(() =>
            {
                Assert.NotNull(Mission.Current);
                Assert.False(mission.EndMissionCalled);
                Assert.Same(destroyedMapEvent, MobileParty.MainParty.MapEvent);
            });
        }

        successor.Call(() =>
        {
            successor.Resolve<IMessageBroker>().Publish(this, new CampaignTick());
            Assert.Null(MobileParty.MainParty.Party.MapEventSide);
        });
    }

    [Fact]
    public void BothPlayersLeave_PvpBattleExitsEncounterMenuForBothPlayers()
    {
        var (ctx, _, _, _) = SetupTwoAlliedPlayersInBattle();

        // Each player sits in a local post-battle encounter at an encounter menu. The close instruction never
        // exits menus itself; it is the map-event-destroy fallback on each client
        // (MapEventRegistry.CloseDestroyedMapEventEncounterIfNeeded) that unwinds the menu left over a battle
        // that no longer exists — once per player, in that player's own scope.
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        using var exitToLast = new GameMenuExitToLastCounter();

        var client1 = Clients.First();
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            client1.Resolve<IMessageBroker>().Publish(this, new MapEventFinalizeAttempted(mapEvent));
        }, MapEventDisabledMethods);

        Assert.Equal(2, exitToLast.Count);
        Assert.Equal(1, exitToLast.CountFor(Clients.First()));
        Assert.Equal(1, exitToLast.CountFor(Clients.Last()));
    }

    [Fact]
    public void RecipientSurrenders_PvpBattleClosesInitiatorEncounterMenu_AndKeepsRecipientCaptive()
    {
        var setup = SetupTwoOpposingPlayersInBattle();

        // Both players sit in a local encounter at the battle's encounter menu when the surrender resolves.
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        string[] closeOnClient1 = null, closeOnClient2 = null;
        Clients.First().Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(p => closeOnClient1 = p.What.PartyIds);
        Clients.Last().Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(p => closeOnClient2 = p.What.PartyIds);

        using var exitToLast = new GameMenuExitToLastCounter();

        var recipientClient = Clients.Last();
        recipientClient.Call(() =>
        {
            Assert.True(recipientClient.ObjectManager.TryGetObject<MapEvent>(setup.ctx.MapEventId, out var mapEvent));
            recipientClient.Resolve<IMessageBroker>().Publish(this, new PlayerSurrendered(mapEvent, MobileParty.MainParty));
        }, BattleMenuSurrenderDisabledMethods());

        AssertMapEventRemoved(Server, setup.ctx.MapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, setup.ctx.MapEventId);

        Assert.NotNull(closeOnClient1);
        Assert.NotNull(closeOnClient2);
        Assert.Contains(setup.initiatorPartyBaseId, closeOnClient1);
        Assert.Contains(setup.recipientPartyBaseId, closeOnClient1);
        Assert.Contains(setup.initiatorPartyBaseId, closeOnClient2);
        Assert.Contains(setup.recipientPartyBaseId, closeOnClient2);

        AssertCaptivity(Server, setup.recipientHeroId, setup.initiatorPartyId);
        foreach (var client in Clients)
            AssertCaptivity(client, setup.recipientHeroId, setup.initiatorPartyId);

        AssertMainPartyLeftBattle(Clients.First());

        // Each player's now-dead encounter menu is exited exactly once, by the map-event-destroy fallback in
        // that player's own scope. (In the live game the surrendered recipient is locally captive by teardown
        // time and the fallback defers to the captivity UI; the harness's test hero is not the local main hero,
        // so its exit runs here too.)
        Assert.Equal(2, exitToLast.Count);
        Assert.Equal(1, exitToLast.CountFor(Clients.First()));
        Assert.Equal(1, exitToLast.CountFor(Clients.Last()));
    }

    /// <summary>
    /// The host playing as the aggressor is a player like any other: when the recipient surrenders, the close
    /// instruction must reach the host's own (server) instance and detach its party from the battle. No menu
    /// exit is forced on the host — the map-event-destroy fallback is a client-side path, and the host's
    /// encounter unwind is driven by its own local <c>PlayerEncounter.Update</c> flow.
    /// </summary>
    [Fact]
    public void RecipientSurrenders_PvpBattleClosesHostAggressorEncounter()
    {
        var setup = SetupTwoOpposingPlayersInBattle();
        SetMainPartyInBattle(Server, setup.ctx.AttackerPartyId);
        EnableHeadlessEncounterFinish(Server);

        string[] closeOnServer = null;
        Server.Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(p => closeOnServer = p.What.PartyIds);

        var recipientClient = Clients.Last();
        recipientClient.Call(() =>
        {
            Assert.True(recipientClient.ObjectManager.TryGetObject<MapEvent>(setup.ctx.MapEventId, out var mapEvent));
            recipientClient.Resolve<IMessageBroker>().Publish(this, new PlayerSurrendered(mapEvent, MobileParty.MainParty));
        }, BattleMenuSurrenderDisabledMethods());

        Assert.NotNull(closeOnServer);
        Assert.Contains(setup.initiatorPartyBaseId, closeOnServer);
        Assert.Contains(setup.recipientPartyBaseId, closeOnServer);
        AssertMainPartyLeftBattle(Server);
    }

    /// <summary>
    /// The host playing as the aggressor leaves the battle: the finalize runs directly on the server, and the
    /// close instruction is also published locally so the host's own party detaches through the same path as
    /// any client's. No menu exit is forced on the host (see
    /// <see cref="RecipientSurrenders_PvpBattleClosesHostAggressorEncounter"/>).
    /// </summary>
    [Fact]
    public void HostAggressorLeave_PvpBattleClosesHostEncounter()
    {
        var setup = SetupTwoOpposingPlayersInBattle();
        SetMainPartyInBattle(Server, setup.ctx.AttackerPartyId);
        EnableHeadlessEncounterFinish(Server);

        string[] closeOnServer = null;
        Server.Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(p => closeOnServer = p.What.PartyIds);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(setup.ctx.MapEventId, out var mapEvent));
            Server.Resolve<IMessageBroker>().Publish(this, new MapEventFinalizeAttempted(mapEvent));
        }, MapEventDisabledMethods);

        Assert.NotNull(closeOnServer);
        Assert.Contains(setup.initiatorPartyBaseId, closeOnServer);
        Assert.Contains(setup.recipientPartyBaseId, closeOnServer);
        AssertMapEventRemoved(Server, setup.ctx.MapEventId);
        AssertMainPartyLeftBattle(Server);
    }

    [Fact]
    public void SpectatorBattleSimulationOpen_ClosesBattleEncounterMenu_WithoutEndingEncounter()
    {
        var setup = SetupTwoOpposingPlayersInBattle();
        var spectatorClient = Clients.Last();
        SetMockPlayerEncounter(spectatorClient, mapEventId: setup.ctx.MapEventId);

        using var exitToLast = new GameMenuExitToLastCounter();

        spectatorClient.Call(() =>
        {
            spectatorClient.Resolve<IMessageBroker>().Publish(this, new NetworkOpenBattleSimulation(setup.ctx.MapEventId));
        }, MapEventDisabledMethods);

        Assert.Equal(1, exitToLast.CountFor(spectatorClient));
        AssertHasPlayerEncounter(spectatorClient, expected: true);
    }

    /// <summary>
    /// The coop auto-finalize on conclusion: both allied players win — a client commits the victory
    /// <see cref="BattleState"/>, the server applies it (OnBattleWon), broadcasts the authoritative battle
    /// results (<c>NetworkCommitMapEventResults</c>) and, recognizing the conclusion (<c>MapEventConcluded</c>),
    /// finalizes the shared <see cref="MapEvent"/> with no explicit leave. Each involved winner's
    /// <c>PlayerEncounter</c> is NOT force-finished: it is staged to <see cref="PlayerEncounterState.CaptureHeroes"/>,
    /// from which the <c>PlayerEncounter.Update</c> path (<c>PlayerEncounterInterface</c>) runs the loot states
    /// and finishes the encounter in the live game. The test triggers NO leave /
    /// <c>MapEventFinalizeAttempted</c> / <c>Finish</c> itself.
    /// </summary>
    [Fact]
    public void BattleConcludesWithVictory_StagesEachWinnersEncounterForBattleResults()
    {
        var (ctx, _, _, _) = SetupTwoAlliedPlayersInBattle();

        // Each player is in its own local post-battle encounter (no attached map event, so Finish's FinalizeBattle
        // is a no-op).
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        // Conclude the battle: the allied attackers win. A client commits the victory BattleState, which the
        // server applies (OnBattleWon). The world-dependent loot/result/capture steps need a live campaign, so
        // disable them. Crucially, the test does NOT trigger any finalize/leave path itself.
        var disabled = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CaptureDefeatedPartyMembers"))
            // Finalizing a battle WITH a winner teleports the defeated parties off the event, which needs a live
            // map scene (the no-winner leave test skips this) — the headless boundary for a concluded finalize.
            .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize"))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            // On a conclusion the winners' clients are still inside their battle mission when the server tears
            // the shared event down, so the map-event-destroy fallback (which finishes an encounter left at a
            // dead menu) defers to the mission flow (MissionState.Current != null) and the staged encounter
            // survives to drive the results screens. Headless there is no mission, so silence the fallback to
            // model that — the staged encounter must NOT be finished, or the results screens would be skipped.
            .Append(AccessTools.Method(typeof(MapEventRegistry), "CloseDestroyedMapEventEncounterIfNeeded"))
            .ToList();

        var client1 = Clients.First();
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            mapEvent.BattleState = BattleState.AttackerVictory;
        }, disabled);

        // The conclusion alone finalized the shared battle — no explicit leave.
        AssertMapEventRemoved(Server, ctx.MapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, ctx.MapEventId);

        // Both winners' encounters survive the close and are staged for the battle-results pass. The commit
        // message carries the winning side from the server, so the ally that never saw the BattleState locally
        // (client SendAll only reaches the server) stages exactly like the committing client.
        AssertPlayerEncounterState(Clients.First(), PlayerEncounterState.CaptureHeroes);
        AssertPlayerEncounterState(Clients.Last(), PlayerEncounterState.CaptureHeroes);
    }

    [Fact]
    public void DuplicateBattleStateChange_AfterServerConclusion_DoesNotPublishSecondClose()
    {
        var (ctx, _, _, _) = SetupTwoAlliedPlayersInBattle();

        int closeCount1 = 0, closeCount2 = 0;
        Clients.First().Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(_ => closeCount1++);
        Clients.Last().Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(_ => closeCount2++);

        var disabled = BattleMenuSurrenderDisabledMethods()
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            mapEvent._battleState = BattleState.AttackerVictory;

            Server.Resolve<IMessageBroker>().Publish(this, new NetworkChangeBattleState(ctx.MapEventId, BattleState.AttackerVictory));
        }, disabled);

        Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out _));
        Assert.Equal(0, closeCount1);
        Assert.Equal(0, closeCount2);
    }

    /// <summary>
    /// The post-migration duplicate finalize (the live -1 roster bug): the host clicks "done" (finalize #1), host
    /// migration promotes another player, and that new host's own "done" sends finalize #2 for the SAME battle.
    /// The server must finalize each map event at most once, or FinalizeEventAux re-forfeits the rosters (the same
    /// troop removed twice -> client roster goes negative). Here both finalizes are real: the first tears the
    /// shared event down and the second is ignored, so the battle finalizes once and each player is closed once.
    /// </summary>
    [Fact]
    public void DuplicateFinalize_AfterMigration_IsIgnored_FinalizesOnce()
    {
        var (ctx, _, _, _) = SetupTwoAlliedPlayersInBattle();

        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        // Count how many times each client is told to close. A duplicate finalize must NOT re-close (re-capture).
        int closeCount1 = 0, closeCount2 = 0;
        Clients.First().Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(_ => closeCount1++);
        Clients.Last().Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(_ => closeCount2++);

        var disabled = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList();

        // Two finalize attempts for the same battle, back to back — the post-migration "both hosts click done".
        var client1 = Clients.First();
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            var broker = client1.Resolve<IMessageBroker>();
            broker.Publish(this, new MapEventFinalizeAttempted(mapEvent));
            broker.Publish(this, new MapEventFinalizeAttempted(mapEvent)); // the duplicate post-migration leave
        }, disabled);

        // Finalized once: the shared event is gone everywhere, and each player was closed exactly once (not twice).
        AssertMapEventRemoved(Server, ctx.MapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, ctx.MapEventId);
        Assert.Equal(1, closeCount1);
        Assert.Equal(1, closeCount2);
    }

    private IReadOnlyList<MethodBase> BattleMenuSurrenderDisabledMethods()
        => MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize"))
            .ToList();

    /// <summary>
    /// Builds a shared battle with two opposing player parties, registers both as players, makes each client's
    /// <see cref="MobileParty.MainParty"/> its own party, and seeds the defender hero so surrender can capture it.
    /// </summary>
    private (
        MapEventContext ctx,
        string initiatorHeroId,
        string recipientHeroId,
        string initiatorPartyId,
        string recipientPartyId,
        string initiatorPartyBaseId,
        string recipientPartyBaseId) SetupTwoOpposingPlayersInBattle()
    {
        var ctx = CreateServerMapEvent();
        var initiatorHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var recipientHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var initiatorPartyBaseId = GetPartyBaseId(Server, ctx.AttackerPartyId);
        var recipientPartyBaseId = GetPartyBaseId(Server, ctx.DefenderPartyId);

        RegisterAsPlayerParty("1", initiatorHeroId, ctx.AttackerPartyId);
        RegisterAsPlayerParty("2", recipientHeroId, ctx.DefenderPartyId);
        PreparePlayerPartyForCapture(recipientHeroId, ctx.DefenderPartyId);

        SetMainPartyInBattle(Clients.First(), ctx.AttackerPartyId);
        SetMainPartyInBattle(Clients.Last(), ctx.DefenderPartyId);
        EnableHeadlessEncounterFinish(Clients.First());
        EnableHeadlessEncounterFinish(Clients.Last());

        return (ctx, initiatorHeroId, recipientHeroId, ctx.AttackerPartyId, ctx.DefenderPartyId, initiatorPartyBaseId, recipientPartyBaseId);
    }

    /// <summary>
    /// Builds a shared battle with two allied players on the winning attacker side and an AI defender on the
    /// losing side, then returns the map-event context, player 2 MobileParty id, and both player PartyBase ids.
    /// </summary>
    private (MapEventContext ctx, string player2PartyId, string player1PartyBaseId, string player2PartyBaseId)
        SetupTwoAlliedPlayersInBattle()
    {
        var ctx = CreateServerMapEvent();

        string attackerSideId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var me));
            Assert.True(Server.ObjectManager.TryGetId(me.AttackerSide, out attackerSideId));
        }, MapEventDisabledMethods);

        // JoinPartyToSide returns the joined MapEventParty id; resolve its MobileParty id to register it as a
        // player, and the PartyBase ids of both players (the close-encounter instruction is keyed by PartyBase).
        var joinedMapEventPartyId = JoinPartyToSide(attackerSideId);
        string player2PartyId = null, player1PartyBaseId = null, player2PartyBaseId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(joinedMapEventPartyId, out var mep));
            Assert.True(Server.ObjectManager.TryGetId(mep.Party.MobileParty, out player2PartyId));
            Assert.True(Server.ObjectManager.TryGetId(mep.Party, out player2PartyBaseId));

            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ctx.AttackerPartyId, out var p1));
            Assert.True(Server.ObjectManager.TryGetId(p1.Party, out player1PartyBaseId));
        }, MapEventDisabledMethods);

        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), ctx.AttackerPartyId);
        RegisterAsPlayerParty("2", TestEnvironment.CreateRegisteredObject<Hero>(), player2PartyId);

        // Each client controls its own party as MainParty (set inside that client's static scope), with the
        // minimal campaign state for PlayerEncounter.Finish to run headless.
        SetMainPartyInBattle(Clients.First(), ctx.AttackerPartyId);
        SetMainPartyInBattle(Clients.Last(), player2PartyId);
        EnableHeadlessEncounterFinish(Clients.First());
        EnableHeadlessEncounterFinish(Clients.Last());

        return (ctx, player2PartyId, player1PartyBaseId, player2PartyBaseId);
    }

    private void PreparePlayerPartyForCapture(string heroId, string partyId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                hero.PartyBelongedTo = party;
            }
        }, MapEventDisabledMethods);
    }

    private static string GetPartyBaseId(EnvironmentInstance instance, string mobilePartyId)
    {
        string partyBaseId = null;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.True(instance.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        Assert.NotNull(partyBaseId);
        return partyBaseId;
    }

    /// <summary>Makes <paramref name="partyId"/> the client's <see cref="MobileParty.MainParty"/> and asserts it
    /// is currently in the battle. Runs in the client's static scope, where <c>Campaign.Current</c> resolves to
    /// that client.</summary>
    private void SetMainPartyInBattle(EnvironmentInstance client, string partyId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
            Assert.Same(party, MobileParty.MainParty);
            Assert.NotNull(MobileParty.MainParty.Party.MapEventSide);
        }, MapEventDisabledMethods);
    }

    private static void AssertMainPartyLeftBattle(EnvironmentInstance client)
    {
        client.Call(() =>
            Assert.Null(MobileParty.MainParty.Party.MapEventSide));
    }

    /// <summary>Asserts the client still has a <c>PlayerEncounter.Current</c> and that it was staged to the
    /// given result state by the server's battle-result commit.</summary>
    private static void AssertPlayerEncounterState(EnvironmentInstance client, PlayerEncounterState expected)
    {
        client.Call(() =>
        {
            Assert.NotNull(PlayerEncounter.Current);
            Assert.Equal(expected, PlayerEncounter.Current.EncounterState);
        });
    }

    private static void AssertMapEventRemoved(EnvironmentInstance instance, string mapEventId)
    {
        instance.Call(() =>
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _),
                $"MapEvent {mapEventId} should be finalized/removed on {instance.GetType().Name}"));
    }

    /// <summary>
    /// Counts <see cref="GameMenu.ExitToLast"/> calls and attributes each to the instance whose container was
    /// active at call time. Attribution is deliberately container-based, not party-based: a handler that sends
    /// a message to another instance mid-flight leaves that instance's game statics behind
    /// (EnvironmentInstance.StaticScope restores the container but not the game statics), so ambient state
    /// like <see cref="MobileParty.MainParty"/> is unreliable inside the prefix while the container is not.
    /// </summary>
    private sealed class GameMenuExitToLastCounter : IDisposable
    {
        private readonly Harmony harmony = new("coop-battle-finalize-exit-to-last-counter");

        public GameMenuExitToLastCounter()
        {
            exitToLastCount = 0;
            exitToLastContainers.Clear();
            harmony.Patch(
                ExitToLastMethod,
                prefix: new HarmonyMethod(typeof(CoopBattleFinalizeTests), nameof(CountGameMenuExitToLast)));
        }

        public int Count => exitToLastCount;

        /// <summary>How many exits ran in the scope of <paramref name="instance"/>.</summary>
        public int CountFor(EnvironmentInstance instance) =>
            exitToLastContainers.Count(container => ReferenceEquals(container, instance.Container));

        public void Dispose()
        {
            harmony.Unpatch(ExitToLastMethod, HarmonyPatchType.Prefix, harmony.Id);
        }
    }

    private static readonly MethodInfo ExitToLastMethod = AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast));
    private static int exitToLastCount;
    private static readonly List<object> exitToLastContainers = new();

    private static bool CountGameMenuExitToLast()
    {
        exitToLastCount++;
        if (GameInterface.ContainerProvider.TryGetContainer(out var container))
        {
            exitToLastContainers.Add(container);
        }

        return false;
    }
}
