using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// The "back up to the encounter" end of a coop battle: two allied players win a shared battle and their
/// <c>PlayerEncounter</c>s must finalize and stay in sync with the server. Each client controls its own
/// <see cref="MobileParty.MainParty"/>, so per-client encounter paths run for real (see
/// <see cref="MapEventTestBase.EnableHeadlessEncounterFinish"/>); only <c>GameMenu.ExitToLast</c> (no menu
/// context exists headless) is mocked.
/// </summary>
public class CoopBattleFinalizeTests : MapEventTestBase
{
    public CoopBattleFinalizeTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// The explicit post-victory leave: the winning side leader finalizes, the server tears down the shared
    /// <see cref="MapEvent"/> and tells both players to close, and each client's <c>PlayerEncounter.Finish</c>
    /// runs headless. This is the wired path and passes.
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
        // detaching it from the finalized battle and clearing the encounter (Finish ran headless).
        AssertMainPartyLeftBattle(Clients.First());
        AssertMainPartyLeftBattle(Clients.Last());
        AssertHasPlayerEncounter(Clients.First(), expected: false);
        AssertHasPlayerEncounter(Clients.Last(), expected: false);
    }

    /// <summary>
    /// The coop auto-finalize on conclusion: a concluded battle finalizes the encounter with no explicit leave.
    /// Both allied players win — a client commits the victory <see cref="BattleState"/>, the server applies it
    /// (OnBattleWon) and, recognizing the conclusion (<c>MapEventConcluded</c>), finalizes the shared
    /// <see cref="MapEvent"/> and tells every involved player to close; each client's <c>PlayerEncounter.Finish</c>
    /// then runs headless. The test triggers NO leave / <c>MapEventFinalizeAttempted</c> / <c>Finish</c> itself.
    /// </summary>
    [Fact]
    public void BattleConcludesWithVictory_PlayerEncounterFinalizesWithoutExplicitLeave()
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

        // The conclusion alone finalized the shared battle and each player's encounter — no explicit leave.
        AssertMapEventRemoved(Server, ctx.MapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, ctx.MapEventId);
        AssertHasPlayerEncounter(Clients.First(), expected: false);
        AssertHasPlayerEncounter(Clients.Last(), expected: false);
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

    private static void AssertMapEventRemoved(EnvironmentInstance instance, string mapEventId)
    {
        instance.Call(() =>
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _),
                $"MapEvent {mapEventId} should be finalized/removed on {instance.GetType().Name}"));
    }
}
