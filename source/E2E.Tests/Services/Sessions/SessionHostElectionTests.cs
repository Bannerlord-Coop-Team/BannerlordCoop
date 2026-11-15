using E2E.Tests.Environment.Instance;
using Common.Network.Messages;
using Coop.Core.Server.Services.Instances.Sessions;
using GameInterface.Services.MapEvents;
using Missions.Messages;
using Missions.Sessions;
using Missions.Sessions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Sessions;

/// <summary>
/// Server-side election, succession and close of a campaign-free session, driven over the mock campaign
/// network: a member announces <c>NetworkMissionEntered</c>, asks for the host, and the server answers per peer.
/// </summary>
public class SessionHostElectionTests : SessionTestEnvironment
{
    public SessionHostElectionTests(ITestOutputHelper output) : base(output, numClients: 3)
    {
    }

    private (string sessionId, EnvironmentInstance[] clients) JoinAll(int count = 3)
    {
        var sessionId = OpenSession();
        var clients = Clients.ToArray();
        string[] ids = { "A", "B", "C" };
        for (int i = 0; i < count; i++)
            JoinSession(clients[i], ids[i], sessionId);
        return (sessionId, clients);
    }

    [Fact]
    public void FirstReadyMember_BecomesHost()
    {
        var (sessionId, clients) = JoinAll();

        RequestSessionHost(clients[0], sessionId);

        foreach (var client in clients)
            AssertSessionHost(client, sessionId, "A", 1);
    }

    [Fact]
    public void HostAssignment_SentPerPeer_NotBroadcast()
    {
        var (sessionId, clients) = JoinAll();

        RequestSessionHost(clients[0], sessionId);

        Assert.Equal(3, Server.NetworkSentMessages.GetMessages<NetworkBattleHostAssigned>().Count());
        Assert.Empty(Server.NetworkBroadcastMessages.GetMessages<NetworkBattleHostAssigned>());
    }

    [Fact]
    public void SecondReadyMember_AppendsSuccessor_SameEpoch()
    {
        var (sessionId, clients) = JoinAll();

        RequestSessionHost(clients[0], sessionId);
        RequestSessionHost(clients[1], sessionId);

        foreach (var client in clients)
            AssertSessionHost(client, sessionId, "A", 1, "B");
    }

    [Fact]
    public void HostDeparture_PromotesSuccessor_EpochAdvances()
    {
        var (sessionId, clients) = JoinAll();
        RequestSessionHost(clients[0], sessionId);
        RequestSessionHost(clients[1], sessionId);
        RequestSessionHost(clients[2], sessionId);

        LeaveSession(clients[0], "A", sessionId);

        AssertSessionHost(clients[1], sessionId, "B", 2, "C");
        AssertSessionHost(clients[2], sessionId, "B", 2, "C");
        AssertIsLocalHost(clients[1], sessionId, true);
    }

    [Fact]
    public void SuccessorDeparture_DropsFromLine_SameEpoch()
    {
        var (sessionId, clients) = JoinAll();
        RequestSessionHost(clients[0], sessionId);
        RequestSessionHost(clients[1], sessionId);
        RequestSessionHost(clients[2], sessionId);

        LeaveSession(clients[1], "B", sessionId);

        AssertSessionHost(clients[0], sessionId, "A", 1, "C");
    }

    [Fact]
    public void EmptyInstance_ClearsHost_SessionStaysOpen()
    {
        var (sessionId, clients) = JoinAll(count: 1);
        RequestSessionHost(clients[0], sessionId);

        LeaveSession(clients[0], "A", sessionId);

        Server.Call(() =>
        {
            Assert.False(Server.Resolve<ISessionHostRegistry>().TryGet(sessionId, out _));
            Assert.Equal(sessionId, Server.Resolve<ISessionDefinitionStore>().Current?.SessionId);
        });

        JoinSession(clients[1], "B", sessionId);
        RequestSessionHost(clients[1], sessionId);

        AssertSessionHost(clients[1], sessionId, "B", 2);
    }

    [Fact]
    public void RequestFromNonMember_Ignored()
    {
        var sessionId = OpenSession();
        var clients = Clients.ToArray();
        JoinSession(clients[0], "A", sessionId);
        SetControllerId(clients[1], "B");
        Server.Call(() =>
        {
            var players = Server.Resolve<GameInterface.Services.Players.IPlayerManager>();
            players.AddPlayer(new GameInterface.Services.Players.Data.Player("B", null, null, null, null));
            players.SetPeer("B", clients[1].NetPeer);
        });

        RequestSessionHost(clients[1], sessionId);

        Server.Call(() => Assert.False(Server.Resolve<ISessionHostRegistry>().TryGet(sessionId, out _)));
    }

    [Fact]
    public void RequestForUnknownSession_Ignored()
    {
        var (sessionId, clients) = JoinAll();
        var otherId = SessionInstanceId.Create();

        RequestSessionHost(clients[0], otherId);

        Server.Call(() =>
        {
            Assert.False(Server.Resolve<ISessionHostRegistry>().TryGet(otherId, out _));
            Assert.False(Server.Resolve<ISessionHostRegistry>().TryGet(sessionId, out _));
        });
    }

    [Fact]
    public void Close_ByHost_EndsMissionForMembers_AndRefusesReentry()
    {
        var (sessionId, clients) = JoinAll();
        RequestSessionHost(clients[0], sessionId);
        RequestSessionHost(clients[1], sessionId);

        RequestSessionClose(clients[0], sessionId, epoch: 1);

        foreach (var member in new[] { clients[0], clients[1], clients[2] })
            Assert.Contains(member.InternalMessages.GetMessages<NetworkEndBattleMission>(), m => m.MapEventId == sessionId);

        Server.Call(() => Assert.Null(Server.Resolve<ISessionDefinitionStore>().Current));

        var endsBefore = clients[2].InternalMessages.GetMessages<NetworkEndBattleMission>().Count();
        Server.SimulateMessage(clients[2].NetPeer, new NetworkMissionEntered("C", sessionId));
        Assert.True(clients[2].InternalMessages.GetMessages<NetworkEndBattleMission>().Count() > endsBefore);
    }

    [Fact]
    public void Close_ByNonHostOrStaleEpoch_Ignored()
    {
        var (sessionId, clients) = JoinAll();
        RequestSessionHost(clients[0], sessionId);
        RequestSessionHost(clients[1], sessionId);

        RequestSessionClose(clients[1], sessionId, epoch: 1);
        RequestSessionClose(clients[0], sessionId, epoch: 7);

        Server.Call(() => Assert.Equal(sessionId, Server.Resolve<ISessionDefinitionStore>().Current?.SessionId));
        Assert.Empty(clients[2].InternalMessages.GetMessages<NetworkEndBattleMission>());
    }

    [Fact]
    public void SessionPlayerDisconnect_DoesNotThrow()
    {
        var (sessionId, clients) = JoinAll();
        foreach (var client in clients)
            RequestSessionHost(client, sessionId);

        // Every PlayerDisconnected subscriber on the real server runs against a player with no hero, party or clan.
        Server.SimulateMessage(clients[0].NetPeer, new PlayerDisconnected(clients[0].NetPeer, default));
        Server.SimulateMessage(clients[1].NetPeer, new PlayerDisconnected(clients[1].NetPeer, default));

        AssertSessionHost(clients[2], sessionId, "C", 3);
    }

    [Fact]
    public void SessionDeparture_DoesNotTouchBattleHostRegistry()
    {
        var (sessionId, clients) = JoinAll();
        RequestSessionHost(clients[0], sessionId);

        LeaveSession(clients[1], "B", sessionId);
        LeaveSession(clients[0], "A", sessionId);

        Server.Call(() => Assert.False(Server.Resolve<IBattleHostRegistry>().TryGet(sessionId, out _)));
    }
}
