using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.Players;
using Missions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>The server ignores battle host and reserve requests whose controller id isn't the sender's own.</summary>
public class BattleHostControllerIdTests : MissionTestEnvironment
{
    public BattleHostControllerIdTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    private EnvironmentInstance ClientA => Clients.ElementAt(0);
    private EnvironmentInstance ClientB => Clients.ElementAt(1);

    [Fact]
    public void HostRequestCarryingAnotherPlayersControllerId_ElectsNoHost()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        LinkPeers();

        Server.SimulateMessage(ClientB.NetPeer, new NetworkRequestBattleHost(mapEventId, "ctrl-A"));
        Server.PumpGameThread();

        AssertNoHost(Server, mapEventId);
    }

    [Fact]
    public void HostRequestWithTheSendersControllerId_ElectsTheSender()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        LinkPeers();

        Server.SimulateMessage(ClientB.NetPeer, new NetworkRequestBattleHost(mapEventId, "ctrl-B"));
        Server.PumpGameThread();

        AssertHost(Server, mapEventId, "ctrl-B");
    }

    [Fact]
    public void ReserveRequestCarryingAnotherPlayersControllerId_SendsNoReserves()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        LinkPeers();
        Server.NetworkSentMessages.Clear();

        Server.SimulateMessage(ClientB.NetPeer, new NetworkRequestBattleReserves(mapEventId, "ctrl-A"));
        Server.PumpGameThread();

        Assert.Empty(Server.NetworkSentMessages.OfType<NetworkBattleTroopReserve>());
    }

    // A real join links each client's peer to its player.
    private void LinkPeers() => Server.Call(() =>
    {
        var playerManager = Server.Resolve<IPlayerManager>();
        playerManager.SetPeer("ctrl-A", ClientA.NetPeer);
        playerManager.SetPeer("ctrl-B", ClientB.NetPeer);
    });
}
