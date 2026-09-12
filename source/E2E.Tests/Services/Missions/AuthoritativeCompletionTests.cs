using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.Players;
using HarmonyLib;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-075 (Authoritative Completion) and BR-076 (Single Finalization). Both are about server-authoritative
/// finalization of a battle result, so they share the same scaffolding: two allied players on the attacker
/// side (one elected host, one non-host) versus an AI defender, with a seeded battle-host assignment and the
/// server-side peer→player mapping the authority gates read.
/// <list type="bullet">
/// <item>BR-075: "Only the authoritative server shall finalize the battle result. A mission host may report
/// that completion conditions have been met, but shall not independently commit persistent campaign results."
/// The untested piece is the non-host conclusion-refusal gate
/// (<c>MapEventHandler.Handle_NetworkChangeBattleState</c>): a non-host's completion report is not applied,
/// while the host's is.</item>
/// <item>BR-076: "A battle result shall be finalized exactly once. Repeated completion messages, host
/// migration, disconnects, or reconnects shall not apply casualties, loot, prisoners, experience, or
/// relationship changes more than once." These tests go beyond the existing back-to-back finalize/battle-state
/// dedup by interleaving an actual host-migration handoff and a reconnect replay, and by asserting the reward
/// COMMIT itself (not just the close instruction) fires exactly once.</item>
/// </list>
/// </summary>
public class AuthoritativeCompletionTests : MapEventTestBase
{
    public AuthoritativeCompletionTests(ITestOutputHelper output) : base(output) { }

    private const string HostControllerId = "host";
    private const string GuestControllerId = "guest";

    // ------------------------------------------------------------------
    // BR-075 — Authoritative Completion
    // ------------------------------------------------------------------

    /// <summary>
    /// A non-host player's local mission can conclude a victory the shared battle never reached. When it
    /// reports that completion to the server (a victory <see cref="NetworkChangeBattleState"/>), the server —
    /// the sole finalizer — must NOT apply it: the battle state stays <see cref="BattleState.None"/> and no
    /// finalize (<c>MapEventConcluded</c>) is triggered. This is the untested non-host conclusion-refusal gate.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-075")]
    public void NonHostVictoryReport_IsNotAppliedByServer()
    {
        var mapEventId = SetupAlliedPlayersWithHost();

        bool concluded = false;
        Server.Resolve<IMessageBroker>().Subscribe<MapEventConcluded>(_ => concluded = true);

        // The non-host (guest) forwards a victory its local mission concluded. Sending it over the network
        // gives the server the sender peer it resolves to the guest player behind the authority gate.
        var nonHostClient = Clients.Last();
        nonHostClient.Call(() =>
            nonHostClient.Resolve<INetwork>().SendAll(new NetworkChangeBattleState(mapEventId, BattleState.AttackerVictory)),
            MapEventDisabledMethods);

        // The server refused the non-host's completion: nothing was finalized, and the battle is still unresolved.
        Assert.False(concluded);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.Equal(BattleState.None, mapEvent.BattleState);
        });
    }

    /// <summary>
    /// The positive control for BR-075: the same victory report, sent by the ELECTED HOST, is honored — the
    /// server (not the host's own report) applies it and finalizes the shared battle exactly once. This proves
    /// the negative test above refuses on the host-authority gate, not because every report is dropped.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-075")]
    public void HostVictoryReport_IsAppliedAndFinalizedByServer()
    {
        var mapEventId = SetupAlliedPlayersWithHost();
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        int concludedCount = 0;
        Server.Resolve<IMessageBroker>().Subscribe<MapEventConcluded>(_ => concludedCount++);

        var hostClient = Clients.First();
        hostClient.Call(() =>
            hostClient.Resolve<INetwork>().SendAll(new NetworkChangeBattleState(mapEventId, BattleState.AttackerVictory)),
            VictoryConclusionDisabledMethods());

        // The host's completion report was honored by the authoritative server, which finalized exactly once.
        Assert.Equal(1, concludedCount);
        AssertMapEventRemoved(Server, mapEventId);
    }

    // ------------------------------------------------------------------
    // BR-076 — Single Finalization
    // ------------------------------------------------------------------

    /// <summary>
    /// Single finalization across an adversarial leave/finalize replay: the host finalizes once, then a
    /// duplicate completion message, a host-migration handoff (host departs, successor promoted), and a
    /// reconnecting client replaying its completion all arrive for the same battle. Each involved player must
    /// be closed exactly once — a second finalize would re-run <c>FinalizeEventAux</c> and re-forfeit rosters.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-076")]
    public void Finalize_IsSingle_AcrossDuplicateMigrationAndReconnect()
    {
        var mapEventId = SetupAlliedPlayersWithHost();
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        int closeOnHost = 0, closeOnGuest = 0;
        Clients.First().Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(_ => closeOnHost++);
        Clients.Last().Resolve<IMessageBroker>().Subscribe<NetworkClosePvpEncounter>(_ => closeOnGuest++);

        var disabled = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList();

        // Keep the shared event object so a finalize can be replayed for it even after it is torn down.
        MapEvent sharedEvent = null;
        var hostClient = Clients.First();
        hostClient.Call(() =>
        {
            Assert.True(hostClient.ObjectManager.TryGetObject<MapEvent>(mapEventId, out sharedEvent));
            var broker = hostClient.Resolve<IMessageBroker>();
            broker.Publish(this, new MapEventFinalizeAttempted(sharedEvent)); // finalize #1 -> closes each player once
            broker.Publish(this, new MapEventFinalizeAttempted(sharedEvent)); // duplicate completion message -> ignored
        }, disabled);

        // A host-migration handoff on the server: the host departs, the successor (guest) is promoted.
        Server.Call(() =>
            Server.Resolve<IMessageBroker>().Publish(this,
                new MissionMemberDeparted(HostControllerId, mapEventId, wasRetreat: false, isInstanceEmpty: false)));

        // A reconnecting/rejoining client replays its completion for the same (now-gone) battle.
        hostClient.Call(() =>
        {
            var broker = hostClient.Resolve<IMessageBroker>();
            broker.Publish(this, new MapEventFinalizeAttempted(sharedEvent)); // reconnect replay -> event gone, no-op
            broker.Publish(this, new MapEventFinalizeAttempted(sharedEvent)); // and once more -> still no-op
        }, disabled);

        // Finalized exactly once: each player was closed exactly once and the event is gone everywhere.
        Assert.Equal(1, closeOnHost);
        Assert.Equal(1, closeOnGuest);
        AssertMapEventRemoved(Server, mapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, mapEventId);
    }

    /// <summary>
    /// The reward COMMIT itself is applied exactly once. The host concludes a victory (the server commits the
    /// result and broadcasts <c>NetworkCommitMapEventResults</c> and <c>MapEventConcluded</c>), then a duplicate
    /// victory report, a host-migration handoff, and a reconnect replay all arrive. The result commit — the
    /// carrier of casualties/loot/prisoners/xp — must fire exactly once, not merely the close instruction.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-076")]
    public void VictoryResult_CommitsExactlyOnce_UnderDuplicateMigrationAndReconnect()
    {
        var mapEventId = SetupAlliedPlayersWithHost();
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        int commitBroadcasts = 0;
        Clients.First().Resolve<IMessageBroker>().Subscribe<NetworkCommitMapEventResults>(_ => commitBroadcasts++);
        int concludedCount = 0;
        Server.Resolve<IMessageBroker>().Subscribe<MapEventConcluded>(_ => concludedCount++);

        var disabled = VictoryConclusionDisabledMethods();

        // The host reports the concluded victory; the server applies it, commits the result, and finalizes.
        var hostClient = Clients.First();
        hostClient.Call(() =>
            hostClient.Resolve<INetwork>().SendAll(new NetworkChangeBattleState(mapEventId, BattleState.AttackerVictory)),
            disabled);

        // Adversarial replay: a duplicate completion message, a host-migration handoff, and a reconnect replay.
        hostClient.Call(() =>
            hostClient.Resolve<INetwork>().SendAll(new NetworkChangeBattleState(mapEventId, BattleState.AttackerVictory)),
            disabled);

        Server.Call(() =>
            Server.Resolve<IMessageBroker>().Publish(this,
                new MissionMemberDeparted(HostControllerId, mapEventId, wasRetreat: false, isInstanceEmpty: false)));

        Clients.Last().Call(() =>
            Clients.Last().Resolve<INetwork>().SendAll(new NetworkChangeBattleState(mapEventId, BattleState.AttackerVictory)),
            disabled);

        // The result was committed and the battle concluded exactly once across the whole replay.
        Assert.Equal(1, commitBroadcasts);
        Assert.Equal(1, concludedCount);
        AssertMapEventRemoved(Server, mapEventId);
    }

    // ------------------------------------------------------------------
    // Setup / helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Stands up a shared battle: an AI defender against two allied players on the attacker side — client 0 is
    /// the elected host (<see cref="HostControllerId"/>), client 1 a non-host successor
    /// (<see cref="GuestControllerId"/>). Seeds the server's <see cref="IBattleHostRegistry"/> with that
    /// assignment and maps each client's peer to its player, so the server's authority gates
    /// (<c>Handle_NetworkChangeBattleState</c>, finalize) can resolve the sender.
    /// </summary>
    /// <returns>The shared battle's map-event id.</returns>
    private string SetupAlliedPlayersWithHost()
    {
        var ctx = CreateServerMapEvent();

        string attackerSideId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var me));
            Assert.True(Server.ObjectManager.TryGetId(me.AttackerSide, out attackerSideId));
        }, MapEventDisabledMethods);

        // A second player party reinforces the attacker side; resolve its MobileParty id to register it.
        var joinedMapEventPartyId = JoinPartyToSide(attackerSideId);
        string guestPartyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(joinedMapEventPartyId, out var mep));
            Assert.True(Server.ObjectManager.TryGetId(mep.Party.MobileParty, out guestPartyId));
        }, MapEventDisabledMethods);

        RegisterAsPlayerParty(HostControllerId, TestEnvironment.CreateRegisteredObject<Hero>(), ctx.AttackerPartyId);
        RegisterAsPlayerParty(GuestControllerId, TestEnvironment.CreateRegisteredObject<Hero>(), guestPartyId);

        // Seed the authoritative host assignment and the server-side peer→player mapping the gates read.
        Server.Call(() =>
        {
            Server.Resolve<IBattleHostRegistry>().Set(ctx.MapEventId,
                new BattleHostAssignment(HostControllerId, new[] { GuestControllerId }));

            var playerManager = Server.Resolve<IPlayerManager>();
            playerManager.SetPeer(HostControllerId, Clients.First().NetPeer);
            playerManager.SetPeer(GuestControllerId, Clients.Last().NetPeer);
        });

        return ctx.MapEventId;
    }

    /// <summary>The BattleConcludesWithVictory boundary: the world-dependent loot/result/capture/finalize steps
    /// need a live campaign scene, so disable them; the victory still applies, broadcasts the result, and
    /// finalizes. <c>GameMenu.ExitToLast</c> and the map-event-destroy encounter fallback (no menu/mission
    /// context headless) are disabled so the close path does not deref a null context.</summary>
    private IReadOnlyList<MethodBase> VictoryConclusionDisabledMethods()
        => MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CaptureDefeatedPartyMembers"))
            .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize"))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .Append(AccessTools.Method(typeof(MapEventRegistry), "CloseDestroyedMapEventEncounterIfNeeded"))
            .ToList();

    private static void AssertMapEventRemoved(EnvironmentInstance instance, string mapEventId)
    {
        instance.Call(() =>
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _),
                $"MapEvent {mapEventId} should be finalized/removed on {instance.GetType().Name}"));
    }
}
