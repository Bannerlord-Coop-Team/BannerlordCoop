using Autofac;
using Common.Messaging;
using Common.Network.Messages;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Core.Server.Services.Instances.Sessions;
using Coop.Tests.Mocks;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States;

public class SessionJoinStateTests
{
    private readonly ServerTestComponent serverComponent;
    private readonly ISessionDefinitionStore definitions;
    private readonly Mock<IPlayerManager> playerManager;
    private readonly NetPeer peer;

    public SessionJoinStateTests(ITestOutputHelper output)
    {
        serverComponent = new ServerTestComponent(output);
        definitions = serverComponent.Container.Resolve<ISessionDefinitionStore>();
        playerManager = serverComponent.Container.Resolve<Mock<IPlayerManager>>();
        peer = serverComponent.Container.Resolve<TestNetwork>().CreatePeer();
    }

    private IConnectionLogic Connect(NetPeer forPeer = null) =>
        serverComponent.Container.Resolve<ConnectionLogic>(new TypedParameter(typeof(NetPeer), forPeer ?? peer));

    private static void Validate(IConnectionLogic connection, NetPeer who, string controllerId)
    {
        var state = (ResolveCharacterState)connection.State;
        state.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(who, new NetworkClientValidate(controllerId)));
    }

    [Fact]
    public void ConnectionLogic_WithSession_StartsAtSessionJoinState()
    {
        definitions.Open("test.mod", new byte[] { 1 });

        Assert.IsType<SessionJoinState>(Connect().State);
    }

    [Fact]
    public void ClientValidate_RegistersNullIdPlayer_AndEntersMissionState()
    {
        var definition = definitions.Open("test.mod", new byte[] { 1, 2, 3 });
        var connection = Connect();

        Validate(connection, peer, "controller-1");

        playerManager.Verify(x => x.AddPlayer(It.Is<Player>(p => p.ControllerId == "controller-1" && p.HeroId == null && p.MobilePartyId == null)), Times.Once);
        playerManager.Verify(x => x.SetPeer("controller-1", peer), Times.Once);
        var joined = Assert.Single(serverComponent.TestNetwork.GetPeerMessagesFromType<NetworkSessionJoined>(peer));
        Assert.True(joined.Accepted);
        Assert.Equal(definition.SessionId, joined.SessionId);
        Assert.Equal("test.mod", joined.ModId);
        Assert.Equal(new byte[] { 1, 2, 3 }, joined.Payload);
        Assert.Equal("controller-1", joined.Player.ControllerId);
        Assert.IsType<MissionState>(connection.State);
    }

    [Fact]
    public void ClientValidate_DoesNotSendExistingPlayers()
    {
        definitions.Open("test.mod", null);
        var connection = Connect();

        Validate(connection, peer, "controller-1");

        Assert.Empty(serverComponent.TestNetwork.GetPeerMessagesFromType<NetworkNewPlayerHeroCreated>(peer));
    }

    [Fact]
    public void ClientValidate_AfterClose_Refuses()
    {
        var definition = definitions.Open("test.mod", null);
        var connection = Connect();
        definitions.Close(definition.SessionId);

        Validate(connection, peer, "controller-1");

        var joined = Assert.Single(serverComponent.TestNetwork.GetPeerMessagesFromType<NetworkSessionJoined>(peer));
        Assert.False(joined.Accepted);
        Assert.False(string.IsNullOrEmpty(joined.Reason));
        Assert.IsType<SessionJoinState>(connection.State);
        playerManager.Verify(x => x.AddPlayer(It.IsAny<Player>()), Times.Never);
    }

    [Fact]
    public void Rejoin_SameController_ReusesPlayer()
    {
        definitions.Open("test.mod", null);
        var registered = new Player("controller-1", null, null, null, null);
        playerManager.Setup(x => x.TryGetPlayer("controller-1", out registered)).Returns(true);
        var secondPeer = serverComponent.Container.Resolve<TestNetwork>().CreatePeer("127.0.0.2");

        Validate(Connect(secondPeer), secondPeer, "controller-1");

        playerManager.Verify(x => x.AddPlayer(It.IsAny<Player>()), Times.Never);
        playerManager.Verify(x => x.SetPeer("controller-1", secondPeer), Times.Once);
        var joined = Assert.Single(serverComponent.TestNetwork.GetPeerMessagesFromType<NetworkSessionJoined>(secondPeer));
        Assert.Same(registered, joined.Player);
    }
}
