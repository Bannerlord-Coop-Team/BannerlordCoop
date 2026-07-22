using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// End-to-end tests for server-authoritative battle host migration: when the host departs the server
/// promotes the earliest-joined successor still present, and a departing non-host successor is dropped from
/// the line. The promotion/cleanup travels the campaign <c>INetwork</c>, which the E2E mock router
/// replicates, so the full server→clients round-trip is exercised. Uses three players to show ordering.
/// </summary>
public class HostMigrationTests : MissionTestEnvironment
{
    public HostMigrationTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void HostDeparts_PromotesFirstSuccessor_OnAllInstances()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        EnterBattle(clients[0], mapEventId); // ctrl-A joins first -> host
        EnterBattle(clients[1], mapEventId); // ctrl-B -> successor
        EnterBattle(clients[2], mapEventId); // ctrl-C -> successor
        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B", "ctrl-C");

        DepartBattle("ctrl-A", mapEventId); // the host leaves

        // The earliest-joined successor is promoted; the rest stay in the line.
        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-C");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-B", "ctrl-C");
    }

    [Fact]
    public void SuccessorDeparts_DropsFromLine_WithoutChangingHost()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[1], mapEventId);
        EnterBattle(clients[2], mapEventId);

        DepartBattle("ctrl-B", mapEventId); // a queued successor leaves, not the host

        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-C");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A", "ctrl-C");
    }

    [Fact]
    public void SuccessiveDepartures_PromoteDownTheJoinOrder()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[1], mapEventId);
        EnterBattle(clients[2], mapEventId);

        DepartBattle("ctrl-A", mapEventId); // host -> promote ctrl-B
        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-C");

        DepartBattle("ctrl-B", mapEventId); // new host -> promote ctrl-C (last one standing)
        AssertHost(Server, mapEventId, "ctrl-C");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-C");
    }

    [Fact]
    public void LastPlayerDeparts_ReleasesMissionModeForSimulation()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var client = Clients.First();
        EnterBattle(client, mapEventId);

        // Model the retreat transition: the mission mode is still recorded, but this harness's main party and
        // PlayerEncounter do not point at the event while the unclaimed update arrives.
        client.Call(() => BattleModeRegistry.Begin(mapEventId, BattleStartMode.Mission));

        Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId)));
        Server.NetworkSentMessages.Clear();

        DepartBattle("ctrl-A", mapEventId, wasRetreat: true, isInstanceEmpty: true);

        var modeChange = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>());
        Assert.Equal(mapEventId, modeChange.MapEventId);
        Assert.Equal((int)BattleStartMode.Unclaimed, modeChange.Mode);
        client.Call(() => Assert.False(BattleModeRegistry.IsMission(mapEventId)));

        Server.Call(() =>
        {
            Assert.True(ServerBattleModeArbiter.TryClaimSimulation(mapEventId));
            ServerBattleModeArbiter.Release(mapEventId);
        });
    }

    /// <summary>
    /// BR-015 Repeated Host Migration: migration walks down the connection order until no players remain. Here
    /// the host leaves (promoting the sole successor) and that just-promoted successor immediately leaves too,
    /// so the migration terminates at empty. With no valid host left, the server clears the host assignment and
    /// (BR-017 handoff) forgets the WHOLE battle's reserves — proven by seeding a reserve the departing
    /// controllers do not own and asserting it too is dropped (ForgetMapEvent, not just ForgetController).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-015")]
    public void MigrationWalksToEmpty_WhenPromotedSuccessorImmediatelyDeparts_RemovesAssignmentAndForgetsReserves()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();
        EnterBattle(clients[0], mapEventId); // ctrl-A joins first -> host
        EnterBattle(clients[1], mapEventId); // ctrl-B -> the sole successor
        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B");

        // Seed a reserve keyed by a party neither leaver owns, so only a whole-battle forget (ForgetMapEvent ->
        // ledger.Remove) — not a per-controller forget — can clear it.
        Server.Call(() => Server.Resolve<IBattleTroopLedger>()
            .SetReserve(mapEventId, "reserve-party", new[] { new TroopReserveEntry(9001, "char-x", 0) }));
        Server.Call(() => Assert.NotEmpty(Server.Resolve<IBattleTroopLedger>().GetParties(mapEventId)));

        // The host leaves: migration promotes the earliest successor still present (the walk continues).
        DepartBattle("ctrl-A", mapEventId);
        AssertHost(Server, mapEventId, "ctrl-B");
        Server.Call(() => Assert.NotEmpty(Server.Resolve<IBattleTroopLedger>().GetParties(mapEventId))); // promotion keeps reserves

        // The just-promoted successor immediately leaves with no one behind it: no valid host remains, so the
        // migration terminates at empty.
        DepartBattle("ctrl-B", mapEventId, wasRetreat: false, isInstanceEmpty: true);

        // Server-authoritative outcome: the host assignment is cleared and the battle's reserves are forgotten.
        Server.Call(() => Assert.False(Server.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out _)));
        Server.Call(() => Assert.Empty(Server.Resolve<IBattleTroopLedger>().GetParties(mapEventId)));
    }

    /// <summary>
    /// BR-035 Reconnection During Host Migration: a reconnecting player shall not interrupt the migration or
    /// reclaim the host out of order — it may become host only per the host-selection rules. After the host
    /// departs and the earliest successor is promoted, the departed player reconnects (re-enters the same
    /// battle) and must land at the END of the successor line (join order), never preempting the migrated host.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-035")]
    public void ReconnectAfterMigration_LandsAtSuccessorTail_WithoutPreemptingHost()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        EnterBattle(clients[0], mapEventId); // ctrl-A -> host
        EnterBattle(clients[1], mapEventId); // ctrl-B -> successor
        EnterBattle(clients[2], mapEventId); // ctrl-C -> successor
        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B", "ctrl-C");

        // The host departs; migration promotes ctrl-B (earliest successor).
        DepartBattle("ctrl-A", mapEventId);
        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-C");

        // The departed player reconnects to the same battle. It must NOT preempt the migrated host; per the
        // join-order host-selection rules it joins at the tail of the successor line, behind ctrl-C.
        EnterBattle(clients[0], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-C", "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-B", "ctrl-C", "ctrl-A");
        AssertIsLocalHost(clients[0], mapEventId, false); // the reconnector did not become host
        AssertIsLocalHost(clients[1], mapEventId, true);  // ctrl-B remains the migrated host
    }
}
