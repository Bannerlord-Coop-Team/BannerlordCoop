using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Configuration;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Battle host election with the server's "preferredBattleHosts" mod-config list (BR-010, BR-013).</summary>
public class PreferredBattleHostTests : MissionTestEnvironment
{
    public PreferredBattleHostTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void PreferredHostStillLoading_IsElectedOverTheFirstReadyPlayer()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-B");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");

        EnterBattle(clients[0], mapEventId);
        AssertNoHost(mapEventId);
        EnterBattle(clients[1], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-B", "ctrl-A");
        Assert.All(Server.NetworkSentMessages.GetMessages<NetworkBattleHostAssigned>(), message => Assert.Equal(1, message.Epoch));
    }

    [Fact]
    public void NoPreferredHosts_ElectsTheFirstReadyPlayerAtOnce()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");

        EnterBattle(clients[0], mapEventId);
        AssertHost(Server, mapEventId, "ctrl-A");
        EnterBattle(clients[1], mapEventId);
        EnterBattle(clients[2], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B", "ctrl-C");
    }

    [Fact]
    public void PreferredHostThatNeverLoads_FallsBackToTheFirstReadyPlayerAfterTheWait()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-C");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");
        Server.Call(() => Server.Resolve<BattleHostHandler>().PreferredHostWait = TimeSpan.Zero);

        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[1], mapEventId);
        AssertNoHost(mapEventId);

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()));

        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A", "ctrl-B");
    }

    [Fact]
    public void PreferredPlayers_GoAheadOfOthersInTheSuccessorLineByRank()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-A", "ctrl-C");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");

        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[1], mapEventId);
        EnterBattle(clients[2], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-C", "ctrl-B");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A", "ctrl-C", "ctrl-B");
    }

    [Fact]
    public void HeldPlayerDisconnects_ThenThePreferredHostLoadsAndHostsAlone()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-C");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");

        EnterBattle(clients[0], mapEventId);
        AssertNoHost(mapEventId);
        Disconnect(clients[0], "ctrl-A", mapEventId);
        EnterBattle(clients[2], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-C");
    }

    [Fact]
    public void HeldPlayerDepartsDuringTheHold_IsNotElected()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-C");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");
        Server.Call(() => Server.Resolve<BattleHostHandler>().PreferredHostWait = TimeSpan.Zero);

        EnterBattle(clients[0], mapEventId);
        DepartBattle("ctrl-A", mapEventId);
        Tick();

        AssertNoHost(mapEventId);
    }

    [Fact]
    public void HigherRankedPreferredHostLoadingLate_WinsOverALowerRankedReadyOne()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-C", "ctrl-B");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");

        EnterBattle(clients[1], mapEventId);
        AssertNoHost(mapEventId);
        EnterBattle(clients[2], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-C", "ctrl-B");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-C", "ctrl-B");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreferredHostThatLeftWhileLoading_DoesNotHoldTheElection(bool wasRetreat)
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-C");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");

        EnterBattle(clients[2], mapEventId, missionReady: false);
        DepartBattle("ctrl-C", mapEventId, wasRetreat);
        EnterBattle(clients[0], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-A");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AwaitedPreferredHostLeaving_EndsTheHoldAtOnce(bool disconnect)
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-C");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");

        EnterBattle(clients[2], mapEventId, missionReady: false);
        EnterBattle(clients[0], mapEventId);
        AssertNoHost(mapEventId);

        if (disconnect)
            Disconnect(clients[2], "ctrl-C", mapEventId);
        else
            DepartBattle("ctrl-C", mapEventId);

        AssertHost(Server, mapEventId, "ctrl-A");
    }

    [Fact]
    public void PreferredHostLeavingMidBattle_MigratesToTheNextByRank()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-B", "ctrl-C");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");

        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[2], mapEventId);
        AssertNoHost(mapEventId);
        EnterBattle(clients[1], mapEventId);
        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-C", "ctrl-A");

        DepartBattle("ctrl-B", mapEventId);

        AssertHost(Server, mapEventId, "ctrl-C", "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-C", "ctrl-A");
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out var assignment));
            Assert.Equal(2, assignment.Epoch);
        });
    }

    [Fact]
    public void HeldElection_ElectsNobodyBeforeTheWaitEnds()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-C");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");

        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[1], mapEventId);
        Tick();

        AssertNoHost(mapEventId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleHostAssigned>());
    }

    [Fact]
    public void DuplicateHeldRequest_KeepsItsMissionReadyPlace()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        SetPreferredHosts("ctrl-C");
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");
        Server.Call(() => Server.Resolve<BattleHostHandler>().PreferredHostWait = TimeSpan.Zero);

        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[1], mapEventId);
        MakeMissionReady(clients[0], mapEventId);
        Tick();

        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B");
    }

    [Fact]
    public void NoPreferredHosts_ElectsTheFirstReadyPlayerWhileOthersLoad()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();
        ConnectPeers(clients, "ctrl-A", "ctrl-B", "ctrl-C");

        EnterBattle(clients[0], mapEventId, missionReady: false);
        EnterBattle(clients[2], mapEventId, missionReady: false);
        EnterBattle(clients[1], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-B");
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleHostAssigned>());
    }

    // What the party-visibility handler does when a peer drops with its party still in the battle.
    private void Disconnect(EnvironmentInstance client, string controllerId, string mapEventId)
    {
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().ClearPeer(client.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(this,
                new PlayerDisconnectedFromMapEvent(controllerId, Server.GetRegisteredObject<MapEvent>(mapEventId)));
        });
    }

    private void Tick()
    {
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()));
    }

    private void SetPreferredHosts(params string[] controllerIds)
    {
        Server.Call(() => Server.Resolve<IModConfig>().Data.ModOptions.PreferredBattleHosts = controllerIds.ToList());
    }

    private void ConnectPeers(EnvironmentInstance[] clients, params string[] controllerIds)
    {
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            for (int i = 0; i < controllerIds.Length; i++)
                playerManager.SetPeer(controllerIds[i], clients[i].NetPeer);
        });
    }

    private void AssertNoHost(string mapEventId)
    {
        Server.Call(() => Assert.False(Server.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out _)));
    }
}
