using Autofac;
using Common.Network.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Interfaces;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections;

public class PeerBroadcastGateTests
{
    private readonly ServerTestComponent serverComponent;
    private readonly IPeerBroadcastGate gate;

    public PeerBroadcastGateTests(ITestOutputHelper output)
    {
        serverComponent = new ServerTestComponent(output);
        gate = serverComponent.Container.Resolve<IPeerBroadcastGate>();
    }

    [Fact]
    public void NewPeer_DoesNotReceiveBroadcasts()
    {
        var peer = serverComponent.TestNetwork.CreatePeer();

        Assert.False(gate.CanBroadcastTo(peer));
    }

    [Fact]
    public void OpenedPeer_ReceivesBroadcasts()
    {
        var peer = serverComponent.TestNetwork.CreatePeer();

        gate.Open(peer);

        Assert.True(gate.CanBroadcastTo(peer));
    }

    [Fact]
    public void DisconnectedPeer_IsClosedAgain()
    {
        var peer = serverComponent.TestNetwork.CreatePeer();
        gate.Open(peer);

        serverComponent.TestMessageBroker.Publish(this, new PlayerDisconnected(peer, default));

        Assert.False(gate.CanBroadcastTo(peer));
    }

    [Fact]
    public void TransferSaveState_OpensTheGate_OnceTheSaveIsSent()
    {
        var peer = serverComponent.TestNetwork.CreatePeer();
        var connectionLogic = serverComponent.Container.Resolve<ConnectionLogic>(new NamedParameter("playerId", peer));

        var saveMock = serverComponent.Container.Resolve<Mock<ISaveInterface>>();
        saveMock.Setup(m => m.SaveCurrentGame()).Returns(new SaveResults(true, new byte[] { 1 }, "campaign"));

        connectionLogic.SetState<TransferSaveState>();

        Assert.True(gate.CanBroadcastTo(peer));
    }

    [Fact]
    public void FailedSaveTransfer_DoesNotOpenTheGate()
    {
        var peer = serverComponent.TestNetwork.CreatePeer();
        var connectionLogic = serverComponent.Container.Resolve<ConnectionLogic>(new NamedParameter("playerId", peer));

        // A failed save disconnects the peer without sending the snapshot, so the gate must
        // stay closed.
        var saveMock = serverComponent.Container.Resolve<Mock<ISaveInterface>>();
        saveMock.Setup(m => m.SaveCurrentGame()).Returns(new SaveResults(false, System.Array.Empty<byte>(), ""));

        connectionLogic.SetState<TransferSaveState>();

        Assert.False(gate.CanBroadcastTo(peer));
    }
}
