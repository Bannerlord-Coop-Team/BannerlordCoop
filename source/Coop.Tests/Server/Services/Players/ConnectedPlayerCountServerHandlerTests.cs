using Common.Network.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Tests.Mocks;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Services.Players;

/// <summary>Tests replication of the server's connected-player count.</summary>
public class ConnectedPlayerCountServerHandlerTests
{
    private readonly ServerTestComponent serverComponent;

    public ConnectedPlayerCountServerHandlerTests(ITestOutputHelper output)
    {
        serverComponent = new ServerTestComponent(output);
    }

    [Fact]
    public void ConnectedPlayersChanged_BroadcastsCountToEveryPeer()
    {
        var firstPeer = serverComponent.TestNetwork.CreatePeer();
        var secondPeer = serverComponent.TestNetwork.CreatePeer();

        serverComponent.TestMessageBroker.Publish(this, new ConnectedPlayersChanged(2));

        Assert.Equal(2, Assert.Single(serverComponent.TestNetwork
            .GetPeerMessagesFromType<NetworkConnectedPlayersChanged>(firstPeer)).ConnectedPlayers);
        Assert.Equal(2, Assert.Single(serverComponent.TestNetwork
            .GetPeerMessagesFromType<NetworkConnectedPlayersChanged>(secondPeer)).ConnectedPlayers);
    }

    [Fact]
    public void PlayerCampaignEntered_SendsLatestCountDirectlyToJoiningPeer()
    {
        var existingPeer = serverComponent.TestNetwork.CreatePeer();
        var joiningPeer = serverComponent.TestNetwork.CreatePeer();
        serverComponent.TestMessageBroker.Publish(this, new ConnectedPlayersChanged(2));
        serverComponent.TestNetwork.Clear();

        serverComponent.TestMessageBroker.Publish(this, new PlayerCampaignEntered(joiningPeer));

        Assert.False(serverComponent.TestNetwork.SentNetworkMessages.ContainsKey(existingPeer.Id));
        Assert.Equal(2, Assert.Single(serverComponent.TestNetwork
            .GetPeerMessagesFromType<NetworkConnectedPlayersChanged>(joiningPeer)).ConnectedPlayers);
    }
}
