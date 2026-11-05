using E2E.Tests.Environment.Instance;
using GameInterface.Services.Players;
using Missions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>The server ignores location host requests whose controller id isn't the sender's own.</summary>
public class LocationHostControllerIdTests : LocationHostTestEnvironment
{
    public LocationHostControllerIdTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    private EnvironmentInstance ClientA => Clients.ElementAt(0);
    private EnvironmentInstance ClientB => Clients.ElementAt(1);

    [Fact]
    public void HostRequestCarryingAnotherPlayersControllerId_ElectsNoHost()
    {
        var (instanceId, _) = SetupSettlementLocation("A", "B");
        LinkPeers();

        Server.SimulateMessage(ClientB.NetPeer, new NetworkRequestLocationHost(instanceId, "A"));
        Server.PumpGameThread();

        AssertNoLocationHost(Server, instanceId);
    }

    [Fact]
    public void HostRequestWithTheSendersControllerId_ElectsTheSender()
    {
        var (instanceId, _) = SetupSettlementLocation("A", "B");
        LinkPeers();

        Server.SimulateMessage(ClientB.NetPeer, new NetworkRequestLocationHost(instanceId, "B"));
        Server.PumpGameThread();

        AssertLocationHost(Server, instanceId, "B");
    }

    // A real join links each client's peer to its player.
    private void LinkPeers() => Server.Call(() =>
    {
        var playerManager = Server.Resolve<IPlayerManager>();
        playerManager.SetPeer("A", ClientA.NetPeer);
        playerManager.SetPeer("B", ClientB.NetPeer);
    });
}
