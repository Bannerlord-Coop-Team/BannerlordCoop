using System.Linq;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Handlers;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using Missions.Battles;
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
            var returnerHeld = returnerDefender.Concat(LatestSideParties(returnerFeeds, BattleSideEnum.Attacker) ?? Array.Empty<string>());
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
            var hostDefenderSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, null, new BattleAgentBudget());
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
            var returnerHeld = returnerAttacker.Concat(LatestSideParties(returnerFeeds, BattleSideEnum.Defender) ?? Array.Empty<string>());
            Assert.Empty(promotedHeld.Intersect(returnerHeld));
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>
    /// BR-031 residual (drop before any host election): a member DISCONNECTS while EVERY participant is still
    /// on the loading screen, so no host has been elected yet. The drop must still mark the member ABSENT — its
    /// still-registered party must fall to the reserve scope of whoever becomes the eventual first-ready host,
    /// exactly as a drop after election does. Otherwise that party is orphaned: no live supplier ever fields
    /// its reinforcements.
    /// <para>
    /// PRE-FIX FAILURE MECHANISM: <c>Handle_MissionMemberDeparted</c> early-returned on the missing host
    /// assignment BEFORE it reached the drop/retreat bookkeeping, so <c>MarkAbsent</c> never ran for a member
    /// that dropped pre-election. When the loading player then becomes the elected host, the reserve build
    /// resolves the dropped member's party to that (still-registered, not-absent) member — not to the host —
    /// so the host's grant feeds the dropper's side EMPTY. The Defender latest-feed therefore does not contain
    /// the dropper's MapEventParty id and <c>Assert.Contains</c> below fails. With the bookkeeping moved above
    /// the assignment check, the dropper is absent-marked, its party falls to the host, and the grant carries
    /// it.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-031")]
    public void MemberDroppingBeforeAnyElection_IsAbsentMarked_SoTheEventualHostInheritsItsParty()
    {
        // c0 becomes the eventual host (attacker side); c1 drops while still loading (defender side).
        var (mapEventId, partyIds) = SetupCoopBattle("eventual-host-ctrl", "dropper-ctrl");
        var clients = Clients.ToArray();
        var eventualHost = clients[0];

        CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        try
        {
            GiveRoster(mapEventId, partyIds[1], ReturnerTroopCount);
            var dropperMepId = GetMapEventPartyId(mapEventId, partyIds[1]);

            // Both participants ENTER but neither becomes mission-ready — no host is elected yet.
            EnterBattle(clients[0], mapEventId, missionReady: false);
            EnterBattle(clients[1], mapEventId, missionReady: false);
            AssertNoHost(Server, mapEventId);

            // The still-loading member disconnects (not a retreat; the instance is not empty — c0 is present).
            DepartBattle("dropper-ctrl", mapEventId, wasRetreat: false, isInstanceEmpty: false);

            // c0 finishes loading and is elected the (first-ready) host.
            int hostBaseline = FeedBaseline(eventualHost, mapEventId);
            MakeMissionReady(eventualHost, mapEventId);
            AssertHost(Server, mapEventId, "eventual-host-ctrl");

            // The elected host must inherit the dropped, still-registered member's party — the drop landed
            // before any election, but was still absent-marked.
            var hostFeeds = FeedsSince(eventualHost, mapEventId, hostBaseline);
            var hostDefender = LatestSideParties(hostFeeds, BattleSideEnum.Defender);
            Assert.NotNull(hostDefender);
            Assert.Contains(dropperMepId, hostDefender);

            // ...at the flattened roster and a fresh (unsupplied) pointer.
            var granted = hostFeeds.Where(feed => feed.Side == (int)BattleSideEnum.Defender)
                .Last().Parties.Single(party => party.PartyId == dropperMepId);
            Assert.Equal(0, granted.SuppliedCount);
            Assert.Equal(ReturnerTroopCount, granted.Entries.Length);
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // BR-033 flush handshake: the host's supplied-progress reports are THROTTLED (~1s), so at the moment a
    // dropped owner returns, the server's ledger can lag the host's true local pointer. Serving the
    // returner from the lagging ledger re-issues descriptors the host already fielded (duplicate agents
    // sharing one UniqueSeed; corrupted seed-keyed casualty attribution). The shrink refresh therefore
    // carries FlushRequested, the host answers each flagged side with an IsFlush progress report holding
    // the dropped parties' FINAL local pointers (captured atomically with the REPLACE), and the server
    // defers the returner's grant until those pointers landed in the ledger — with fallbacks so the
    // returner is NEVER stranded (host departure serves it; a tick-driven deadline serves it).
    // ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Stands up the BR-033 race window: the returner drops, the host adopts its party through the real
    /// wire grant into a REAL registered defender-side supplier, fields <paramref name="reportedToLedger"/>
    /// troops and reports them (the server's ledger pointer advances), then fields more troops inside the
    /// reporter's throttle window WITHOUT a report — the host's true local pointer
    /// (<paramref name="suppliedLocally"/>) is now AHEAD of the server's ledger.
    /// </summary>
    private (string mapEventId, string returnerMepId, CoopTroopSupplier hostDefenderSupplier) SetupHostAheadOfLedger(
        int reportedToLedger, int suppliedLocally)
    {
        var (mapEventId, partyIds) = SetupCoopBattle("host-ctrl", "returner-ctrl");
        var clients = Clients.ToArray();
        var host = clients[0];

        GiveRoster(mapEventId, partyIds[1], ReturnerTroopCount);
        var returnerMepId = GetMapEventPartyId(mapEventId, partyIds[1]);

        EnterBattle(clients[0], mapEventId); // first mission-ready -> host
        EnterBattle(clients[1], mapEventId); // successor
        DepartBattle("returner-ctrl", mapEventId, wasRetreat: false);

        // The registry is process-static: drop the returner's own buffered entry/election feeds so the
        // host-labeled supplier below is populated only by the grant actually addressed to the HOST.
        CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        var hostDefenderSupplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, null, new BattleAgentBudget());
        CoopTroopSupplierRegistry.Register(hostDefenderSupplier);

        // The host adopts (records its live peer on the server) and its supplier receives the grant.
        RequestOwnedReserves(host, mapEventId, "host-ctrl");
        Assert.True(hostDefenderSupplier.IsPopulated,
            "the adoption grant must populate the host's supplier with the dropped player's reserve");
        Assert.Equal(ReturnerTroopCount, hostDefenderSupplier.TotalTroops);

        // The host fields part of the adopted reserve and REPORTS it, exactly as SupplyProgressReporter
        // does — the server's ledger pointer advances to reportedToLedger...
        hostDefenderSupplier.SupplyTroops(reportedToLedger);
        var progress = hostDefenderSupplier.GetSuppliedByParty()
            .Select(party => new SupplyProgressEntry(party.partyId, party.supplied))
            .ToArray();
        host.Call(() => host.Resolve<INetwork>().SendAll(new NetworkBattleSupplyProgress(mapEventId, progress)));

        // ...then fields MORE inside the reporter's ~1s throttle window (no report goes out): the host's
        // true local pointer is now AHEAD of the server's ledger. This is the shrink-refresh race window.
        hostDefenderSupplier.SupplyTroops(suppliedLocally - reportedToLedger);

        return (mapEventId, returnerMepId, hostDefenderSupplier);
    }

    /// <summary>
    /// BR-033 (throttled-report race): the host supplied PAST its last progress report when the dropped
    /// owner returns. The returner's grant must resume at the host's TRUE local pointer — obtained via the
    /// flush handshake on the shrink refresh — not at the lagging ledger value; otherwise the descriptors
    /// the host fielded in the report gap are re-issued and spawn twice (PuppetSpawner dedups by AgentId,
    /// not by UniqueSeed).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void ReEntryGrant_ResumesFromTheHostsTruePointer_WhenTheLedgerLagsItsReports()
    {
        const int reportedToLedger = 1;
        const int suppliedLocally = 3;

        var (mapEventId, returnerMepId, _) = SetupHostAheadOfLedger(reportedToLedger, suppliedLocally);
        var returner = Clients.ToArray()[1];
        try
        {
            // The returner re-enters through the real entry + election flow.
            int returnerBaseline = FeedBaseline(returner, mapEventId);
            EnterBattle(returner, mapEventId);

            var returnerFeeds = FeedsSince(returner, mapEventId, returnerBaseline);
            var defenderFeeds = returnerFeeds.Where(feed => feed.Side == (int)BattleSideEnum.Defender).ToArray();
            Assert.True(defenderFeeds.Length > 0, "the returner must still be served its side on re-entry");
            var resumed = defenderFeeds.Last().Parties.Single(party => party.PartyId == returnerMepId);

            // The grant resumes at the host's TRUE pointer (post-flush), not the stale ledger value.
            Assert.Equal(suppliedLocally, resumed.SuppliedCount);
            Assert.Equal(ReturnerTroopCount, resumed.Entries.Length);

            // No descriptor is served twice: the seeds the host already fielded must not be in the tail
            // the returner will field from its resumed pointer.
            var hostFieldedSeeds = resumed.Entries.Take(suppliedLocally).Select(entry => entry.Seed);
            var returnerTailSeeds = resumed.Entries.Skip(resumed.SuppliedCount).Select(entry => entry.Seed);
            Assert.Empty(hostFieldedSeeds.Intersect(returnerTailSeeds));
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>
    /// BR-033 (ack-before-grant ordering): the host's IsFlush progress must land in the LEDGER before the
    /// returner's grant is computed. If the grant were computed first (or the flush never applied), the
    /// grant's pointer and the ledger's post-return pointer would diverge — here both must equal the
    /// host's true pointer.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void FlushedPointers_LandInTheLedger_BeforeTheReturnersGrantIsComputed()
    {
        const int reportedToLedger = 1;
        const int suppliedLocally = 3;

        var (mapEventId, returnerMepId, _) = SetupHostAheadOfLedger(reportedToLedger, suppliedLocally);
        var returner = Clients.ToArray()[1];
        try
        {
            int returnerBaseline = FeedBaseline(returner, mapEventId);
            EnterBattle(returner, mapEventId);

            // The flush landed in the authoritative ledger...
            int ledgerPointer = -1;
            Server.Call(() =>
            {
                Assert.True(Server.Resolve<IBattleTroopLedger>()
                    .TryGetReserve(mapEventId, returnerMepId, out _, out ledgerPointer));
            });
            Assert.Equal(suppliedLocally, ledgerPointer);

            // ...and BEFORE the grant was computed: the grant carries that same caught-up pointer.
            var defenderFeeds = FeedsSince(returner, mapEventId, returnerBaseline)
                .Where(feed => feed.Side == (int)BattleSideEnum.Defender).ToArray();
            Assert.True(defenderFeeds.Length > 0, "the returner must still be served its side on re-entry");
            var resumed = defenderFeeds.Last().Parties.Single(party => party.PartyId == returnerMepId);
            Assert.Equal(ledgerPointer, resumed.SuppliedCount);
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>
    /// BR-033 fallback (host departs): the shrink refresh went out but the host DISCONNECTS before its
    /// flush ack arrives. The pending return must not strand the returner — the host's departure serves it
    /// from the current ledger (the last REPORTED pointer; the unreported supplies vanished with the host,
    /// today's race accepted). The unresponsive host is modeled by disposing its client-side reserve
    /// handler, so the flagged refresh is delivered but never processed or acked.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void HostDepartingBeforeTheFlushAck_ServesThePendingReturner_FromTheLedger()
    {
        const int reportedToLedger = 1;
        const int suppliedLocally = 3;

        var (mapEventId, returnerMepId, _) = SetupHostAheadOfLedger(reportedToLedger, suppliedLocally);
        var clients = Clients.ToArray();
        var host = clients[0];
        var returner = clients[1];
        try
        {
            // The host goes unresponsive: the flagged shrink refresh will be delivered but never acked.
            host.Call(() => host.Resolve<BattleTroopReserveHandler>().Dispose());

            // The returner re-enters: its grant is DEFERRED behind the flush handshake — no reserve feed yet.
            int returnerBaseline = FeedBaseline(returner, mapEventId);
            EnterBattle(returner, mapEventId);
            Assert.Empty(FeedsSince(returner, mapEventId, returnerBaseline));

            // The host drops. Its departure must complete the pending return: the returner is served from
            // the current ledger instead of waiting for an ack that can no longer come.
            DepartBattle("host-ctrl", mapEventId, wasRetreat: false);

            var defenderFeeds = FeedsSince(returner, mapEventId, returnerBaseline)
                .Where(feed => feed.Side == (int)BattleSideEnum.Defender).ToArray();
            Assert.True(defenderFeeds.Length > 0,
                "the host's departure must serve the pending return — the returner is never stranded");
            var resumed = defenderFeeds.Last().Parties.Single(party => party.PartyId == returnerMepId);
            Assert.Equal(reportedToLedger, resumed.SuppliedCount); // the ledger value — the un-acked tail is lost with the host
            Assert.Equal(ReturnerTroopCount, resumed.Entries.Length);
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>
    /// BR-033 fallback (deadline): the flush ack never arrives (a legacy host that ignores FlushRequested,
    /// or a lost message) and the host never departs. A server-side, campaign-tick-driven deadline must
    /// serve the pending returner from the current ledger — the returner is never stranded. The deadline is
    /// set to zero so the first tick after the deferral expires it without wall-clock waits.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void FlushAckNeverArriving_ServesThePendingReturner_AfterTheDeadline()
    {
        const int reportedToLedger = 1;
        const int suppliedLocally = 3;

        var (mapEventId, returnerMepId, _) = SetupHostAheadOfLedger(reportedToLedger, suppliedLocally);
        var clients = Clients.ToArray();
        var host = clients[0];
        var returner = clients[1];
        try
        {
            // A legacy/unresponsive host: applies nothing, acks nothing.
            host.Call(() => host.Resolve<BattleTroopReserveHandler>().Dispose());

            // Expire pendings on the first tick after creation.
            Server.Call(() => Server.Resolve<BattleHostHandler>().FlushAckDeadline = TimeSpan.Zero);

            int returnerBaseline = FeedBaseline(returner, mapEventId);
            EnterBattle(returner, mapEventId);
            Assert.Empty(FeedsSince(returner, mapEventId, returnerBaseline)); // deferred, no tick yet

            // The campaign tick sweeps the expired pending and serves the returner from the ledger.
            Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()));

            var defenderFeeds = FeedsSince(returner, mapEventId, returnerBaseline)
                .Where(feed => feed.Side == (int)BattleSideEnum.Defender).ToArray();
            Assert.True(defenderFeeds.Length > 0,
                "the deadline must serve the pending return — the returner is never stranded");
            var resumed = defenderFeeds.Last().Parties.Single(party => party.PartyId == returnerMepId);
            Assert.Equal(reportedToLedger, resumed.SuppliedCount);
            Assert.Equal(ReturnerTroopCount, resumed.Entries.Length);
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>
    /// BR-033 legacy client contract: an UNFLAGGED shrink (FlushRequested = false — what today's server, or
    /// a legacy one, sends) behaves exactly as before: the REPLACE applies and NO flush ack is sent. A
    /// FLAGGED shrink acks each message once with the dropped parties' final local pointers (IsFlush set),
    /// even when the REPLACE drops nothing.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void UnflaggedShrink_AppliesTheReplaceWithoutAcking_AndFlaggedShrinkAcksDroppedPointers()
    {
        const string mapEventId = "flush_contract_battle";
        const string returnedParty = "returned-party";
        const string keptParty = "kept-party";

        var client = Clients.First();

        TroopReserveEntry[] Entries(int count, int seedBase) =>
            Enumerable.Range(0, count).Select(i => new TroopReserveEntry(seedBase + i, $"char_{seedBase + i}", 0)).ToArray();
        PartyReserve[] BothParties() => new[]
        {
            new PartyReserve(returnedParty, 0, Entries(4, seedBase: 100)),
            new PartyReserve(keptParty, 0, Entries(3, seedBase: 200)),
        };
        PartyReserve[] ShrunkToKept() => new[] { new PartyReserve(keptParty, 0, Entries(3, seedBase: 200)) };

        CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        try
        {
            var supplier = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, null, new BattleAgentBudget());
            CoopTroopSupplierRegistry.Register(supplier);
            supplier.SetReserve(BothParties());
            supplier.SupplyTroops(2); // advance "returned-party" locally to 2

            int AckCount() => client.NetworkSentMessages.GetMessages<NetworkBattleSupplyProgress>()
                .Count(message => message.MapEventId == mapEventId);
            int ackBaseline = AckCount();

            // LEGACY: an unflagged shrink applies the REPLACE (the returned party leaves the supplier)
            // but sends NO ack — exactly today's behavior.
            client.SimulateMessage(Server.NetPeer,
                new NetworkBattleTroopReserve(mapEventId, (int)BattleSideEnum.Defender, ShrunkToKept()));
            Assert.Equal(3, supplier.TotalTroops); // only kept-party remains
            Assert.Equal(ackBaseline, AckCount());

            // FLAGGED: re-seed and advance again, then shrink with FlushRequested — exactly ONE ack, with
            // IsFlush set, carrying the dropped party's FINAL local pointer.
            supplier.SetReserve(BothParties());
            supplier.SupplyTroops(2);
            client.SimulateMessage(Server.NetPeer,
                new NetworkBattleTroopReserve(mapEventId, (int)BattleSideEnum.Defender, ShrunkToKept(), flushRequested: true));

            var acks = client.NetworkSentMessages.GetMessages<NetworkBattleSupplyProgress>()
                .Where(message => message.MapEventId == mapEventId)
                .Skip(ackBaseline)
                .ToArray();
            var ack = Assert.Single(acks);
            Assert.True(ack.IsFlush);
            var flushed = Assert.Single(ack.Entries);
            Assert.Equal(returnedParty, flushed.PartyId);
            Assert.Equal(2, flushed.SuppliedCount);
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>
    /// Wire safety of the additive handshake fields: both flags survive a real protobuf round trip (and
    /// default to false, so unflagged/legacy traffic is unchanged on the wire).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-033")]
    public void FlushHandshakeFields_RoundTripOverTheWire()
    {
        var reserve = Server.EnsureSerializable(new NetworkBattleTroopReserve(
            "rt_battle", (int)BattleSideEnum.Attacker, Array.Empty<PartyReserve>(), flushRequested: true));
        Assert.True(reserve.FlushRequested);

        var legacyReserve = Server.EnsureSerializable(new NetworkBattleTroopReserve(
            "rt_battle", (int)BattleSideEnum.Attacker, Array.Empty<PartyReserve>()));
        Assert.False(legacyReserve.FlushRequested);

        var flush = Server.EnsureSerializable(new NetworkBattleSupplyProgress(
            "rt_battle", new[] { new SupplyProgressEntry("party", 2) }, isFlush: true));
        Assert.True(flush.IsFlush);
        Assert.Equal(2, flush.Entries.Single().SuppliedCount);

        var legacyReport = Server.EnsureSerializable(new NetworkBattleSupplyProgress(
            "rt_battle", new[] { new SupplyProgressEntry("party", 2) }));
        Assert.False(legacyReport.IsFlush);
    }
}
