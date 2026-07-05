using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
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
        EnterBattle(Clients.First(), mapEventId);

        Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId)));
        Server.NetworkSentMessages.Clear();

        DepartBattle("ctrl-A", mapEventId, wasRetreat: true, isInstanceEmpty: true);

        var modeChange = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>());
        Assert.Equal(mapEventId, modeChange.MapEventId);
        Assert.Equal((int)BattleStartMode.Unclaimed, modeChange.Mode);

        Server.Call(() =>
        {
            Assert.True(ServerBattleModeArbiter.TryClaimSimulation(mapEventId));
            ServerBattleModeArbiter.Release(mapEventId);
        });
    }
}
