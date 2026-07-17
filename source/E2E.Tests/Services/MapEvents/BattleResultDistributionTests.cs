using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Data;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.TroopRosters.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Section 9 (Battle Results) distribution rules: the authoritative final result is attributed to the correct
/// participating party (BR-081) and reaches every participating player (BR-082). The result is packed keyed by the
/// stable <see cref="MapEventParty"/> coop id (<see cref="MapEventResultsInterface"/>), broadcast to all clients
/// (<c>NetworkCommitMapEventResults</c>), and each client's <c>MapEventResultsHandler</c> stages ONLY its own
/// party's loot onto its <see cref="PlayerEncounter"/> — so loot never cross-attributes between two winners on the
/// same side. Actual loot <em>content</em> production needs the live <c>BattleRewardModel</c> (GameModels), so
/// these tests feed the per-party loot payload directly to exercise the keying/attribution/delivery path headless.
/// </summary>
public class BattleResultDistributionTests : MapEventTestBase
{
    public BattleResultDistributionTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// BR-081 (unit-level): the loot package round-trips keyed by the stable <see cref="MapEventParty"/> coop id.
    /// Two winning parties each carry distinct looted members and prisoners; after Pack -> wire -> Unpack each
    /// party gets back exactly its own troops and never the other party's — proving the keying does not
    /// cross-attribute.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-081")]
    public void PackUnpackPlayerLootData_RoundTripsEachPartysLoot_KeyedByStablePartyId_NoCrossAttribution()
    {
        // Two independent participating parties and two distinct troop characters, registered on every instance.
        var partyAId = TestEnvironment.CreateRegisteredObject<MapEventParty>();
        var partyBId = TestEnvironment.CreateRegisteredObject<MapEventParty>();
        var troopAId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var troopBId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(partyAId, out var partyA));
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(partyBId, out var partyB));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopAId, out var troopA));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopBId, out var troopB));

            var results = Server.Resolve<IMapEventResultsInterface>();

            var lootedMembers = new Dictionary<MapEventParty, TroopRoster>();
            var lootedPrisoners = new Dictionary<MapEventParty, TroopRoster>();
            using (new AllowedThread())
            {
                // Party A: 2 of troop A as freed members, 1 of troop B as prisoners.
                lootedMembers[partyA] = BuildRoster(troopA, 2);
                lootedPrisoners[partyA] = BuildRoster(troopB, 1);

                // Party B: 3 of troop B as freed members, 1 of troop A as prisoners.
                lootedMembers[partyB] = BuildRoster(troopB, 3);
                lootedPrisoners[partyB] = BuildRoster(troopA, 1);
            }

            var original = new PlayerLootData(
                new Dictionary<MapEventParty, ItemRoster>(),
                lootedMembers,
                lootedPrisoners);

            // Pack to the wire form (keyed by coop id) and unpack it back.
            NetworkPlayerLootData packed = results.PackPlayerLootData(original);
            PlayerLootData unpacked = results.UnpackPlayerLootData(packed);

            // Each party's members came back attributed to itself — and not to the other party.
            AssertRosterHolds(unpacked.LootedMembers, partyA, troopA, 2, notContaining: troopB);
            AssertRosterHolds(unpacked.LootedMembers, partyB, troopB, 3, notContaining: troopA);

            // Prisoners likewise stay on the party that captured them.
            AssertRosterHolds(unpacked.LootedPrisoners, partyA, troopB, 1, notContaining: troopA);
            AssertRosterHolds(unpacked.LootedPrisoners, partyB, troopA, 1, notContaining: troopB);
        }, MapEventDisabledMethods);
    }

    /// <summary>
    /// BR-081 (end-to-end): two allied player parties win the same battle. The authoritative result is broadcast
    /// with distinct prisoners per party; each winner's <see cref="PlayerEncounter"/> must receive ONLY its own
    /// party's prisoners. This is the apply-side attribution filter (<c>MapEventResultsHandler</c> matches
    /// <c>MapEventParty.Party == PartyBase.MainParty</c>) that keeps one winner's loot off another winner.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-081")]
    public void CommittedResults_EachWinnerReceivesOnlyItsOwnPartysPrisoners()
    {
        var (ctx, player2PartyId) = SetupTwoAlliedPlayersOnAttackerSide();

        // Distinct prisoners for each winner — the loot content the broadcast carries.
        var troopForP1 = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var troopForP2 = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        // Each winner sits in its own post-battle encounter to receive its loot.
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));

            var mep1Id = ResolveMapEventPartyId(mapEvent.AttackerSide, ctx.AttackerPartyId);
            var mep2Id = ResolveMapEventPartyId(mapEvent.AttackerSide, player2PartyId);
            Assert.NotEqual(mep1Id, mep2Id);

            var lootedPrisoners = new Dictionary<string, TroopRosterData>
            {
                { mep1Id, new TroopRosterData(new[] { new TroopRosterElementData(troopForP1, 1, 0, 0) }) },
                { mep2Id, new TroopRosterData(new[] { new TroopRosterElementData(troopForP2, 1, 0, 0) }) },
            };

            var payload = new NetworkPlayerLootData(
                new Dictionary<string, ItemRosterElement[]>(),
                new Dictionary<string, TroopRosterData>(),
                lootedPrisoners);

            // The authoritative server broadcasts the per-party result to every client.
            Server.Resolve<INetwork>().SendAll(
                new NetworkCommitMapEventResults(ctx.MapEventId, BattleSideEnum.Attacker, payload));
        }, MapEventDisabledMethods);

        // Player 1's encounter received only player 1's prisoner; player 2's received only player 2's.
        AssertEncounterPrisoners(Clients.First(), ownTroopId: troopForP1, foreignTroopId: troopForP2);
        AssertEncounterPrisoners(Clients.Last(), ownTroopId: troopForP2, foreignTroopId: troopForP1);
    }

    /// <summary>
    /// BR-082: every participating player receives the authoritative final result. The battle concludes as an
    /// allied player victory (a client commits the victory <see cref="BattleState"/>, the server applies it and
    /// broadcasts <c>NetworkCommitMapEventResults</c>). Both allies must receive the broadcast carrying the
    /// server's authoritative winning side — including the ally that never saw the local <see cref="BattleState"/>
    /// change — and both stage their encounter for the results pass. Loot <em>content</em> needs live GameModels;
    /// here we assert delivery + the authoritative winning side + result staging, which are drivable headless.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-082")]
    public void ConcludedPlayerVictory_BroadcastsAuthoritativeResult_ToEveryParticipatingAlly()
    {
        var (ctx, _) = SetupTwoAlliedPlayersOnAttackerSide();

        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        // Every participating client watches for the authoritative result broadcast.
        BattleSideEnum? winningSideOnClient1 = null, winningSideOnClient2 = null;
        int commitsOnClient1 = 0, commitsOnClient2 = 0;
        Clients.First().Resolve<IMessageBroker>().Subscribe<NetworkCommitMapEventResults>(p =>
        {
            commitsOnClient1++;
            winningSideOnClient1 = p.What.WinningSide;
        });
        Clients.Last().Resolve<IMessageBroker>().Subscribe<NetworkCommitMapEventResults>(p =>
        {
            commitsOnClient2++;
            winningSideOnClient2 = p.What.WinningSide;
        });

        // Conclude the battle: the allied attackers win. One client commits the victory BattleState, the server
        // applies it (OnBattleWon) and broadcasts the results. The world-dependent loot/capture steps need a live
        // campaign, so disable them — the test asserts only broadcast delivery, not loot content.
        var client1 = Clients.First();
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            mapEvent.BattleState = BattleState.AttackerVictory;
        }, ConcludeVictoryDisabledMethods());

        // Both allied winners received the broadcast exactly once, carrying the server's authoritative winning
        // side — even the ally that never set the BattleState locally.
        Assert.Equal(1, commitsOnClient1);
        Assert.Equal(1, commitsOnClient2);
        Assert.Equal(BattleSideEnum.Attacker, winningSideOnClient1);
        Assert.Equal(BattleSideEnum.Attacker, winningSideOnClient2);

        // ...and each staged its own encounter for the battle-results pass.
        AssertPlayerEncounterState(Clients.First(), PlayerEncounterState.CaptureHeroes);
        AssertPlayerEncounterState(Clients.Last(), PlayerEncounterState.CaptureHeroes);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>The disabled-methods set for concluding a player victory headless (mirrors the world-dependent
    /// loot/capture/finalize steps that need a live map scene + GameModels).</summary>
    private IReadOnlyList<MethodBase> ConcludeVictoryDisabledMethods()
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

    /// <summary>
    /// Builds a shared field battle with two allied player parties on the attacker side (the original attacker and
    /// one reinforcing party), registers both as players, and makes each client's <see cref="MobileParty.MainParty"/>
    /// its own party in the battle. Returns the map-event context and the second player's MobileParty id.
    /// </summary>
    private (MapEventContext ctx, string player2PartyId) SetupTwoAlliedPlayersOnAttackerSide()
    {
        var ctx = CreateServerMapEvent();

        string attackerSideId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var me));
            Assert.True(Server.ObjectManager.TryGetId(me.AttackerSide, out attackerSideId));
        }, MapEventDisabledMethods);

        var joinedMapEventPartyId = JoinPartyToSide(attackerSideId);

        string player2PartyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(joinedMapEventPartyId, out var mep));
            Assert.True(Server.ObjectManager.TryGetId(mep.Party.MobileParty, out player2PartyId));
        }, MapEventDisabledMethods);

        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), ctx.AttackerPartyId);
        RegisterAsPlayerParty("2", TestEnvironment.CreateRegisteredObject<Hero>(), player2PartyId);

        SetMainPartyInBattle(Clients.First(), ctx.AttackerPartyId);
        SetMainPartyInBattle(Clients.Last(), player2PartyId);
        EnableHeadlessEncounterFinish(Clients.First());
        EnableHeadlessEncounterFinish(Clients.Last());

        return (ctx, player2PartyId);
    }

    /// <summary>Makes <paramref name="partyId"/> the client's <see cref="MobileParty.MainParty"/> and asserts it is
    /// currently in the battle (runs in the client's static scope, where <c>Campaign.Current</c> is that client).</summary>
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

    /// <summary>Resolves the coop id of the <see cref="MapEventParty"/> on <paramref name="side"/> whose mobile
    /// party has coop id <paramref name="mobilePartyId"/>.</summary>
    private string ResolveMapEventPartyId(MapEventSide side, string mobilePartyId)
    {
        foreach (var mapEventParty in side.Parties)
        {
            if (!Server.ObjectManager.TryGetId(mapEventParty.Party?.MobileParty, out var id)) continue;
            if (id != mobilePartyId) continue;

            Assert.True(Server.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));
            return mapEventPartyId;
        }

        Assert.Fail($"No MapEventParty on the side wraps mobile party {mobilePartyId}");
        return null;
    }

    private static TroopRoster BuildRoster(CharacterObject character, int count)
    {
        var roster = new TroopRoster();
        roster.AddToCounts(character, count);
        return roster;
    }

    private static void AssertRosterHolds(
        Dictionary<MapEventParty, TroopRoster> rosters,
        MapEventParty party,
        CharacterObject expectedTroop,
        int expectedCount,
        CharacterObject notContaining)
    {
        Assert.True(rosters.TryGetValue(party, out var roster), "party lost its loot entry through the round-trip");
        Assert.True(roster.Contains(expectedTroop), "party's own troop is missing after the round-trip");
        Assert.False(roster.Contains(notContaining), "another party's troop cross-attributed into this party's loot");
        Assert.Equal(expectedCount, roster.TotalManCount);
    }

    private void AssertEncounterPrisoners(EnvironmentInstance client, string ownTroopId, string foreignTroopId)
    {
        client.Call(() =>
        {
            Assert.NotNull(PlayerEncounter.Current);
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(ownTroopId, out var ownTroop));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(foreignTroopId, out var foreignTroop));

            var prisoners = PlayerEncounter.Current.RosterToReceiveLootPrisoners;
            Assert.True(prisoners.Contains(ownTroop), "winner did not receive its own party's prisoner");
            Assert.False(prisoners.Contains(foreignTroop), "winner received another party's prisoner (cross-attribution)");
            Assert.Equal(1, prisoners.TotalManCount);
        });
    }

    private static void AssertPlayerEncounterState(EnvironmentInstance client, PlayerEncounterState expected)
    {
        client.Call(() =>
        {
            Assert.NotNull(PlayerEncounter.Current);
            Assert.Equal(expected, PlayerEncounter.Current.EncounterState);
        });
    }
}
