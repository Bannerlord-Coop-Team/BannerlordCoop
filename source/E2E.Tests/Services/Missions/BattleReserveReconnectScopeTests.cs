using System.Linq;
using Common.Network;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// The UNSPAWNED-RESERVE half of the disconnect -> adopt -> reconnect round trip. Fielded-agent control is
/// covered by <see cref="BattleReconnectControlTests"/>; these tests cover the server's troop-reserve ledger
/// scope across the same membership churn:
/// <list type="bullet">
/// <item>BR-020/BR-031: when a player DROPS (not retreats) mid-battle, its parties fall into the HOST's
/// reserve scope — the host's adoption-time re-request (<see cref="NetworkRequestBattleReserves"/>) is served
/// the dropped player's parties at the current ledger pointers, so the host can field their remaining
/// reinforcements.</item>
/// <item>BR-033: when the player RE-ENTERS, the server re-issues the CURRENT owned set to BOTH sides of the
/// handoff — the returner gets its parties at the advanced pointer (no troop fielded twice or skipped), and
/// the holder (host, or promoted host after a migration chain) gets a refresh WITHOUT the returned parties
/// (same <see cref="NetworkBattleTroopReserve"/> REPLACE semantics), so no two suppliers ever hold the same
/// party's reserve.</item>
/// </list>
/// The flows are driven through the real wire path (entry/election requests via <c>EnterBattle</c>, the
/// server-observed departure via <c>DepartBattle</c>, and the holder's re-request exactly as
/// <c>BattleAuthorityMigrator.RequestReserves</c> sends it). Reserve scope is asserted on the per-instance
/// RECEIVED <see cref="NetworkBattleTroopReserve"/> messages (baseline-relative — the shared process-static
/// supplier registry cannot represent two clients at once), and the wire-to-supplier REPLACE semantics are
/// locked in by <see cref="CoopTroopSupplierTests"/>.
/// </summary>
public class BattleReserveReconnectScopeTests : MissionTestEnvironment
{
    private const int ReturnerTroopCount = 4;

    public BattleReserveReconnectScopeTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    /// <summary>The MapEventParty id wrapping a player's party — the key the reserve ledger uses.</summary>
    private string GetMapEventPartyId(string mapEventId, string partyId)
    {
        string mepId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var mep = mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties)
                .Last(p => p.Party == party.Party);
            Assert.True(Server.ObjectManager.TryGetId(mep, out mepId));
        }, MapEventDisabledMethods);
        Assert.NotNull(mepId);
        return mepId;
    }

    /// <summary>Give a party a known battle-ready roster on the server (what the reserve flatten reads),
    /// replacing the nondeterministic harness roster so entry counts are assertable.</summary>
    private void GiveRoster(string mapEventId, string partyId, int troopCount)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var mep = mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties)
                .Last(p => p.Party == party.Party);

            var troop = Server.CreateRegisteredObject<CharacterObject>($"reserve_scope_troop_{partyId}");
            mep.Party.MemberRoster.Clear();
            mep.Party.MemberRoster.AddToCounts(troop, troopCount);
            mep.Update();
        }, MapEventDisabledMethods);
    }

    /// <summary>Count of this battle's reserve feeds RECEIVED so far by an instance (the baseline for
    /// baseline-relative asserts).</summary>
    private static int FeedBaseline(EnvironmentInstance instance, string mapEventId)
        => instance.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Count(message => message.MapEventId == mapEventId);

    /// <summary>This battle's reserve feeds an instance received AFTER the baseline snapshot.</summary>
    private static NetworkBattleTroopReserve[] FeedsSince(EnvironmentInstance instance, string mapEventId, int baseline)
        => instance.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Where(message => message.MapEventId == mapEventId)
            .Skip(baseline)
            .ToArray();

    /// <summary>The party ids of the LATEST feed for one side (the supplier state after REPLACE), or null
    /// when that side was never (re-)fed in the observed window.</summary>
    private static string[] LatestSideParties(NetworkBattleTroopReserve[] feeds, BattleSideEnum side)
    {
        var sideFeeds = feeds.Where(feed => feed.Side == (int)side).ToArray();
        return sideFeeds.Length == 0 ? null : sideFeeds.Last().Parties.Select(party => party.PartyId).ToArray();
    }

    /// <summary>Re-request the requester's owned reserves over the real wire — the exact message
    /// <c>BattleAuthorityMigrator.RequestReserves</c> sends after adopting a departed owner's parties
    /// (host adoption on a drop; promoted-host adoption + orphan sweep on a migration).</summary>
    private static void RequestOwnedReserves(EnvironmentInstance client, string mapEventId, string controllerId)
    {
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestBattleReserves(mapEventId, controllerId)));
    }

    /// <summary>
    /// BR-020/BR-031 (reserve half): "returner-ctrl" drops mid-battle. The host's adoption-time reserve
    /// re-request must be served the DROPPED player's party — at the current ledger pointer — alongside the
    /// host's own scope, so the host can field the leaver's remaining reinforcements while they are away.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-020")]
    public void DroppedPlayersParties_FallToTheHostsReserveScope_AtCurrentPointers()
    {
        // "host-ctrl" = attacker side, "returner-ctrl" = defender side.
        var (mapEventId, partyIds) = SetupCoopBattle("host-ctrl", "returner-ctrl");
        var clients = Clients.ToArray();
        var host = clients[0];

        CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        try
        {
            GiveRoster(mapEventId, partyIds[1], ReturnerTroopCount);
            var hostMepId = GetMapEventPartyId(mapEventId, partyIds[0]);
            var returnerMepId = GetMapEventPartyId(mapEventId, partyIds[1]);

            EnterBattle(clients[0], mapEventId); // first mission-ready -> host
            EnterBattle(clients[1], mapEventId); // successor
            AssertHost(Server, mapEventId, "host-ctrl", "returner-ctrl");

            // The returner DROPS (server-observed, not a retreat -> the reserve pointer is kept).
            DepartBattle("returner-ctrl", mapEventId, wasRetreat: false);
            AssertHost(Server, mapEventId, "host-ctrl");

            // The host adopts and pulls its updated owned set, exactly as the adoption path does.
            int baseline = FeedBaseline(host, mapEventId);
            RequestOwnedReserves(host, mapEventId, "host-ctrl");

            var feeds = FeedsSince(host, mapEventId, baseline);

            // The dropped player's (defender-side) party is granted to the host at the fresh pointer...
            var defenderParties = feeds.Where(feed => feed.Side == (int)BattleSideEnum.Defender).ToArray();
            Assert.True(defenderParties.Length > 0,
                "the adoption re-request must be served the dropped player's side (its parties fall to the host while it is away)");
            var granted = defenderParties.Last().Parties.SingleOrDefault(party => party.PartyId == returnerMepId);
            Assert.True(granted != null, "the dropped player's party must be in the host's granted reserve");
            Assert.Equal(0, granted.SuppliedCount);
            Assert.Equal(ReturnerTroopCount, granted.Entries.Length);

            // ...and the host's own party stays in scope.
            var attackerParties = LatestSideParties(feeds, BattleSideEnum.Attacker);
            Assert.NotNull(attackerParties);
            Assert.Contains(hostMepId, attackerParties);
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>
    /// BR-033 (reserve half): after the drop + host adoption, the player RE-ENTERS through the same entry
    /// flow. The returner must be granted exactly its own party back (at the current pointer), and the HOST
    /// must receive a scope REFRESH that no longer carries the returned party — the REPLACE re-feed is what
    /// makes its supplier drop the reserve, so the returner's reinforcements cannot be fielded twice. On no
    /// instance may both latest feeds carry the same party.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void ReturningPlayersParties_LeaveTheHostsReserveScope_OnReEntry()
    {
        var (mapEventId, partyIds) = SetupCoopBattle("host-ctrl", "returner-ctrl");
        var clients = Clients.ToArray();
        var host = clients[0];
        var returner = clients[1];

        CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        try
        {
            GiveRoster(mapEventId, partyIds[1], ReturnerTroopCount);
            var hostMepId = GetMapEventPartyId(mapEventId, partyIds[0]);
            var returnerMepId = GetMapEventPartyId(mapEventId, partyIds[1]);

            EnterBattle(clients[0], mapEventId);
            EnterBattle(clients[1], mapEventId);

            // The returner drops; the host adopts and is granted the returner's party.
            DepartBattle("returner-ctrl", mapEventId, wasRetreat: false);
            RequestOwnedReserves(host, mapEventId, "host-ctrl");

            // The returner re-enters (entry reserve request + mission-ready election request).
            int hostBaseline = FeedBaseline(host, mapEventId);
            int returnerBaseline = FeedBaseline(returner, mapEventId);
            EnterBattle(returner, mapEventId);
            AssertHost(Server, mapEventId, "host-ctrl", "returner-ctrl");

            // The returner's grant: exactly its own party, resumed at the ledger pointer (0 — nothing was
            // fielded while it was away in this test).
            var returnerFeeds = FeedsSince(returner, mapEventId, returnerBaseline);
            var returnerDefender = LatestSideParties(returnerFeeds, BattleSideEnum.Defender);
            Assert.NotNull(returnerDefender);
            var returned = Assert.Single(returnerDefender);
            Assert.Equal(returnerMepId, returned);

            // The HOST's scope shrank: it must be RE-FED its current owned set without the returned party
            // (REPLACE semantics clear it from the supplier). Nothing else ever tells the host to drop it.
            var hostFeeds = FeedsSince(host, mapEventId, hostBaseline);
            Assert.True(hostFeeds.Length > 0,
                "the host's reserve scope must be refreshed when the dropped owner returns — otherwise two suppliers hold the same party");
            var hostDefender = LatestSideParties(hostFeeds, BattleSideEnum.Defender);
            Assert.NotNull(hostDefender);
            Assert.DoesNotContain(returnerMepId, hostDefender);

            // The host keeps its OWN party (isolation: the refresh shrinks, it must not strip unrelated scope).
            var hostAttacker = LatestSideParties(hostFeeds, BattleSideEnum.Attacker);
            Assert.NotNull(hostAttacker);
            Assert.Contains(hostMepId, hostAttacker);

            // Exclusivity: across the two clients' latest per-side feeds, no party is held twice.
            var hostHeld = hostAttacker.Concat(hostDefender);
            var returnerHeld = returnerDefender.Concat(LatestSideParties(returnerFeeds, BattleSideEnum.Attacker) ?? new string[0]);
            Assert.Empty(hostHeld.Intersect(returnerHeld));
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>
    /// BR-033, partial-supply: while the owner is away the host fields PART of the adopted reserve through
    /// the real supply path (supplier pointer advance + the periodic progress report the reporter sends).
    /// The returner's re-entry grant must resume at the ADVANCED ledger pointer — already-fielded troops are
    /// not re-issued (no double-spawn) and the unfielded tail is intact (no skipped troops).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void ReEntryGrant_ResumesFromTheAdvancedLedgerPointer_AfterHostSuppliedPart()
    {
        const int suppliedWhileAway = 2;

        var (mapEventId, partyIds) = SetupCoopBattle("host-ctrl", "returner-ctrl");
        var clients = Clients.ToArray();
        var host = clients[0];
        var returner = clients[1];

        CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        try
        {
            GiveRoster(mapEventId, partyIds[1], ReturnerTroopCount);
            var returnerMepId = GetMapEventPartyId(mapEventId, partyIds[1]);

            EnterBattle(clients[0], mapEventId);
            EnterBattle(clients[1], mapEventId);
            DepartBattle("returner-ctrl", mapEventId, wasRetreat: false);

            // The supplier registry is process-static, so the RETURNER's own entry/election feeds are still
            // buffered under this battle's key. Drop them, so the host-labeled supplier below can only be
            // populated by a feed actually addressed to the HOST (the adoption grant under test).
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);

            // The host's defender-side supplier (the side it now fields the returner's party on) receives the
            // adoption grant for real, so the supply-pointer advance goes through the actual supplier.
            var hostDefenderSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, null);
            CoopTroopSupplierRegistry.Register(hostDefenderSupplier);

            RequestOwnedReserves(host, mapEventId, "host-ctrl");
            Assert.True(hostDefenderSupplier.IsPopulated,
                "the adoption grant must populate the host's supplier with the dropped player's reserve");
            Assert.Equal(ReturnerTroopCount, hostDefenderSupplier.TotalTroops);

            // The host fields part of the adopted reserve, then reports progress exactly as
            // SupplyProgressReporter does — the server's ledger pointer advances monotonically.
            hostDefenderSupplier.SupplyTroops(suppliedWhileAway);
            var progress = hostDefenderSupplier.GetSuppliedByParty()
                .Select(party => new SupplyProgressEntry(party.partyId, party.supplied))
                .ToArray();
            host.Call(() => host.Resolve<INetwork>().SendAll(new NetworkBattleSupplyProgress(mapEventId, progress)));

            // Drop the observation supplier before the re-entry burst (the registry is process-static, so the
            // returner's grant would otherwise land in the host-labeled supplier).
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);

            // The returner re-enters: its grant must carry the full entry list at the ADVANCED pointer.
            int returnerBaseline = FeedBaseline(returner, mapEventId);
            EnterBattle(returner, mapEventId);

            var returnerFeeds = FeedsSince(returner, mapEventId, returnerBaseline);
            var defenderFeeds = returnerFeeds.Where(feed => feed.Side == (int)BattleSideEnum.Defender).ToArray();
            Assert.True(defenderFeeds.Length > 0, "the returner must be granted its own side on re-entry");
            var resumed = defenderFeeds.Last().Parties.Single(party => party.PartyId == returnerMepId);
            Assert.Equal(suppliedWhileAway, resumed.SuppliedCount);           // no double-supply of fielded troops
            Assert.Equal(ReturnerTroopCount, resumed.Entries.Length);         // no skipped tail
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>
    /// BR-033 across a migration chain: C drops (host H adopts + is granted C's party), then H drops and S is
    /// promoted — S's adoption/sweep re-request is served EVERY absent owner's parties (H's and C's). When C
    /// then re-enters, S's refresh drops exactly C's party while keeping the still-absent H's party and S's
    /// own scope (isolation: a connected player's reserve is untouched throughout).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void MigrationChain_PromotedHostDropsOnlyTheReturnersParties_OnReEntry()
    {
        // "first-host" + "returner" = attacker side, "promoted-host" = defender side.
        var (mapEventId, partyIds) = SetupCoopBattle("first-host", "promoted-host", "returner");
        var clients = Clients.ToArray();
        var promoted = clients[1];
        var returner = clients[2];

        CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        try
        {
            GiveRoster(mapEventId, partyIds[2], ReturnerTroopCount);
            var firstHostMepId = GetMapEventPartyId(mapEventId, partyIds[0]);
            var promotedMepId = GetMapEventPartyId(mapEventId, partyIds[1]);
            var returnerMepId = GetMapEventPartyId(mapEventId, partyIds[2]);

            EnterBattle(clients[0], mapEventId); // "first-host" -> host
            EnterBattle(clients[1], mapEventId); // "promoted-host" -> first successor
            EnterBattle(clients[2], mapEventId); // "returner" -> second successor
            AssertHost(Server, mapEventId, "first-host", "promoted-host", "returner");

            // C drops, then H drops: the server promotes S.
            DepartBattle("returner", mapEventId, wasRetreat: false);
            DepartBattle("first-host", mapEventId, wasRetreat: false);
            AssertHost(Server, mapEventId, "promoted-host");

            // S's promotion re-request (adoption + orphan sweep): it must now hold BOTH absent owners' parties.
            int promotedBaseline = FeedBaseline(promoted, mapEventId);
            RequestOwnedReserves(promoted, mapEventId, "promoted-host");

            var grantFeeds = FeedsSince(promoted, mapEventId, promotedBaseline);
            var grantedAttacker = LatestSideParties(grantFeeds, BattleSideEnum.Attacker);
            Assert.NotNull(grantedAttacker);
            Assert.Contains(firstHostMepId, grantedAttacker);
            Assert.Contains(returnerMepId, grantedAttacker);
            var grantedDefender = LatestSideParties(grantFeeds, BattleSideEnum.Defender);
            Assert.NotNull(grantedDefender);
            Assert.Contains(promotedMepId, grantedDefender);

            // C re-enters against the promoted host.
            promotedBaseline = FeedBaseline(promoted, mapEventId);
            int returnerBaseline = FeedBaseline(returner, mapEventId);
            EnterBattle(returner, mapEventId);

            // C is granted exactly its own party back — never the still-absent H's.
            var returnerFeeds = FeedsSince(returner, mapEventId, returnerBaseline);
            var returnerAttacker = LatestSideParties(returnerFeeds, BattleSideEnum.Attacker);
            Assert.NotNull(returnerAttacker);
            Assert.Contains(returnerMepId, returnerAttacker);
            Assert.DoesNotContain(firstHostMepId, returnerAttacker);

            // S's refresh drops exactly C's party: H's stays (still absent), S's own scope is untouched.
            var refreshFeeds = FeedsSince(promoted, mapEventId, promotedBaseline);
            Assert.True(refreshFeeds.Length > 0,
                "the promoted host's reserve scope must be refreshed when the dropped owner returns");
            var refreshedAttacker = LatestSideParties(refreshFeeds, BattleSideEnum.Attacker);
            Assert.NotNull(refreshedAttacker);
            Assert.DoesNotContain(returnerMepId, refreshedAttacker);
            Assert.Contains(firstHostMepId, refreshedAttacker);
            var refreshedDefender = LatestSideParties(refreshFeeds, BattleSideEnum.Defender);
            Assert.NotNull(refreshedDefender);
            Assert.Contains(promotedMepId, refreshedDefender);

            // Exclusivity after the handoff: no party appears in both clients' latest scopes.
            var promotedHeld = refreshedAttacker.Concat(refreshedDefender);
            var returnerHeld = returnerAttacker.Concat(LatestSideParties(returnerFeeds, BattleSideEnum.Defender) ?? new string[0]);
            Assert.Empty(promotedHeld.Intersect(returnerHeld));
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }
}
