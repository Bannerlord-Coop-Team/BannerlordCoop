using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.Players;
using Missions.Messages;
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

#if DEBUG
    [Fact]
    public void FixtureReadyDeferral_CancelIsBoundAndIdempotent()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var client = Clients.First();
        client.Call(() =>
        {
            var handler = client.Resolve<global::Missions.Battles.BattleHostHandler>();
            Assert.False(handler.DeferFixtureMissionReady(mapEventId, "ctrl-A"));
            Assert.True(handler.DeferFixtureMissionReady(mapEventId, "ctrl-B"));
            Assert.False(handler.CancelFixtureMissionReady("other-event"));
            Assert.True(handler.CancelFixtureMissionReady(mapEventId));
            Assert.True(handler.CancelFixtureMissionReady(mapEventId));
        });
        EnterBattle(client, mapEventId);
        AssertHost(Server, mapEventId, "ctrl-A");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FixtureReadyDeferral_WaitsForActualElectionThenJoinsAtEpochOne(bool deferredClientLoadsFirst)
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();
        clients[0].Call(() => Assert.True(clients[0].Resolve<global::Missions.Battles.BattleHostHandler>()
            .DeferFixtureMissionReady(mapEventId, "ctrl-B")));
        if (deferredClientLoadsFirst)
        {
            EnterBattle(clients[0], mapEventId);
            Assert.Empty(clients[0].NetworkSentMessages.GetMessages<NetworkRequestBattleHost>());
        }
        EnterBattle(clients[1], mapEventId);
        if (!deferredClientLoadsFirst) EnterBattle(clients[0], mapEventId);
        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-A");
        Assert.Single(clients[0].NetworkSentMessages.GetMessages<NetworkRequestBattleHost>());
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-B", "ctrl-A");
        Assert.All(Server.NetworkSentMessages.GetMessages<NetworkBattleHostAssigned>(), message => Assert.Equal(1, message.Epoch));
    }
#endif

    [Fact]
    public void Election_PicksFirstToJoin_AppendsSuccessorsOnce_AndMarksOnlyTheHostLocal()
    {
        // Join order, not id order, decides the host: ctrl-B enters first even though "ctrl-A" sorts lower.
        var (mapEventId, _) = SetupCoopBattle("ctrl-B", "ctrl-A");

        EnterBattle(Clients.First(), mapEventId);
        EnterBattle(Clients.Last(), mapEventId);
        EnterBattle(Clients.Last(), mapEventId);   // re-entry must not duplicate the successor or move the host

        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-B", "ctrl-A");

        var clients = Clients.ToArray();
        AssertIsLocalHost(clients[0], mapEventId, true);
        AssertIsLocalHost(clients[1], mapEventId, false);
    }

    [Fact]
    public void InitialElection_PublishesLocalAuthorityAcquired()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var host = Clients.First();
        int acquired = 0;
        host.Resolve<IMessageBroker>().Subscribe<BattleHostAuthorityAcquired>(payload =>
        {
            if (payload.What.MapEventId == mapEventId)
                acquired++;
        });

        EnterBattle(host, mapEventId);

        Assert.Equal(1, acquired);
    }

    [Fact]
    public void InitialElection_QueuesAssignmentBeforeServerReconciliation()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        bool assignmentWasQueued = false;
        Server.Resolve<IMessageBroker>().Subscribe<BattleHostAssignmentChanged>(payload =>
        {
            if (payload.What.MapEventId == mapEventId)
            {
                assignmentWasQueued = Server.NetworkSentMessages
                    .GetMessages<NetworkBattleHostAssigned>()
                    .Any(message => message.MapEventId == mapEventId);
            }
        });

        EnterBattle(Clients.First(), mapEventId);

        Assert.True(assignmentWasQueued);
    }

    // The second client switched seats and still sends the first player's controller id.
    [Fact]
    public void HostRequestCarryingAnotherSeatsControllerId_ElectsNoHost()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer("ctrl-B", Clients.Last().NetPeer));

        Server.SimulateMessage(Clients.Last().NetPeer, new NetworkRequestBattleHost(mapEventId, "ctrl-A"));
        Server.PumpGameThread();

        AssertNoHost(Server, mapEventId);
    }

    [Fact]
    public void ReserveRequestCarryingAnotherSeatsControllerId_SendsNoReserves()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer("ctrl-B", Clients.Last().NetPeer));
        Server.NetworkSentMessages.Clear();

        Server.SimulateMessage(Clients.Last().NetPeer, new NetworkRequestBattleReserves(mapEventId, "ctrl-A"));
        Server.PumpGameThread();

        Assert.Empty(Server.NetworkSentMessages.OfType<NetworkBattleTroopReserve>());
    }

    // The sender left before its queued request ran, so its peer no longer stands for any seat.
    [Fact]
    public void HostRequestFromAPeerThatLeftBeforeItRan_ElectsNoHost()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var playerManager = Server.Resolve<IPlayerManager>();
        Server.Call(() => playerManager.SetPeer("ctrl-A", Clients.First().NetPeer));

        Server.SimulateMessage(Clients.First().NetPeer, new NetworkRequestBattleHost(mapEventId, "ctrl-A"), markGameThread: false);
        Server.Call(() => playerManager.ClearPeer(Clients.First().NetPeer));
        Server.PumpGameThread();

        AssertNoHost(Server, mapEventId);
    }
}
