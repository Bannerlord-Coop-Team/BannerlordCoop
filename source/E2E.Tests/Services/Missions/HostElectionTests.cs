using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// End-to-end tests for server-authoritative battle host election (Phase 1). The election travels the
/// campaign <c>INetwork</c>, which the E2E mock router replicates, so the full client→server→clients
/// round-trip is exercised here.
/// </summary>
public class HostElectionTests : MissionTestEnvironment
{
    public HostElectionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Election_PicksFirstToJoin_AsHost_OnAllInstances()
    {
        // client0 = "ctrl-B", client1 = "ctrl-A". Join order, not id order, decides the host: client0
        // (ctrl-B) enters first, so it is the host even though "ctrl-A" sorts lower.
        var (mapEventId, _) = SetupCoopBattle("ctrl-B", "ctrl-A");

        EnterBattle(Clients.First(), mapEventId);  // ctrl-B joins first -> host
        EnterBattle(Clients.Last(), mapEventId);   // ctrl-A joins next -> successor

        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-B", "ctrl-A");
    }

    [Fact]
    public void Election_MarksOnlyTheHostClient_AsLocalHost()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-B", "ctrl-A");

        EnterBattle(Clients.First(), mapEventId);  // ctrl-B joins first -> host
        EnterBattle(Clients.Last(), mapEventId);

        var clients = Clients.ToArray();
        AssertIsLocalHost(clients[0], mapEventId, true);  // ctrl-B joined first, so it is the host
        AssertIsLocalHost(clients[1], mapEventId, false); // ctrl-A is a successor
    }

    [Fact]
    public void Election_AppendsSuccessors_InJoinOrder_AndIsIdempotent()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");

        EnterBattle(Clients.First(), mapEventId);  // ctrl-A -> host
        EnterBattle(Clients.Last(), mapEventId);   // ctrl-B -> successor
        EnterBattle(Clients.Last(), mapEventId);   // ctrl-B re-enters: no duplicate, host unchanged

        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A", "ctrl-B");
    }
}
