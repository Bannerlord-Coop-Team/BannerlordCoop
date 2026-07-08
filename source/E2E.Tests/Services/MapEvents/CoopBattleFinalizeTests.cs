using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// The "back up to the encounter" end of a coop battle: two allied players win a shared battle, the server
/// finalizes the shared <see cref="MapEvent"/> and each player's <c>PlayerEncounter</c> is detached and staged
/// for the battle-results pass (it is no longer force-finished; the <c>PlayerEncounter.Update</c> path drives
/// the loot states and the finish in the live game). Each client controls its own
/// <see cref="MobileParty.MainParty"/>, so per-client encounter paths run for real (see
/// <see cref="MapEventTestBase.EnableHeadlessEncounterFinish"/>); only <c>GameMenu.ExitToLast</c> (no menu
/// context exists headless) is mocked.
/// </summary>
public class CoopBattleFinalizeTests : MapEventTestBase
{
    public CoopBattleFinalizeTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// The explicit post-victory leave: the winning side leader finalizes, the server tears down the shared
    /// <see cref="MapEvent"/>, replies to the leaver with <c>NetworkMapEventFinalized</c> (which finishes the
    /// leaver's own encounter) and tells both players to close. Closing detaches each player's MainParty from
    /// the battle but deliberately does NOT finish the local <c>PlayerEncounter</c> anymore: the ally's stays
    /// open so the player backs out through the (now unblocked) encounter menu, with any pending battle-result
    /// states driven by the <c>PlayerEncounter.Update</c> path (see
    /// <c>PvPInteractionClientHandler.CloseEncounter</c> and <c>PlayerEncounterPatches.UpdatePrefix</c>).
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
        // runs for real; only the finalize handler's unconditional GameMenu.ExitToLast is mocked.
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

        // The leaver's own encounter is finished by its NetworkMapEventFinalized reply (the explicit-leave
        // teardown in BattleFinalizeHandler). The ally only receives the close instruction, which detaches its
        // party but deliberately leaves the encounter open: the player backs out of the encounter menu
        // themselves (GameMenuEncounterLeaveOnConditionPatch unblocks the leave once the MapEvent is gone).
        AssertHasPlayerEncounter(Clients.First(), expected: false);
        AssertHasPlayerEncounter(Clients.Last(), expected: true);
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

    /// <summary>
    /// Builds a shared battle with two allied players on the winning (attacker) side and the AI defender on the
    /// losing side, registers both as players, makes each client's <see cref="MobileParty.MainParty"/> its own
    /// party (asserting it is in the battle), and enables headless <c>PlayerEncounter.Finish</c> on both clients.
    /// Returns the map-event context, player 2's MobileParty id, and both players' PartyBase ids.
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
}
