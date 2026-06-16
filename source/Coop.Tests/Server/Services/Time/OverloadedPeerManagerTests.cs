using Autofac;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Services.Time;

public class OverloadedPeerManagerTests
{
    private readonly ServerTestComponent serverComponent;

    private TestNetwork TestNetwork => serverComponent.TestNetwork;

    // Thresholds come from NetworkConfig: pause above MaxPacketsInQueue (10000), resume only once every
    // peer is back below ResumePacketsInQueue (5000).
    private const int AbovePauseThreshold = 15000;
    private const int BetweenThresholds = 7000;
    private const int BelowResumeThreshold = 4000;

    public OverloadedPeerManagerTests(ITestOutputHelper output)
    {
        serverComponent = new ServerTestComponent(output);
    }

    [Fact]
    public void OverloadedPeer_PausesTimeOnce()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = serverComponent.Container.Resolve<IOverloadedPeerManager>();

        var peer = AddConnectedPeer(connections);
        peer.SetQueueLength(AbovePauseThreshold);

        // Act
        manager.CheckForOverloadedPeers();

        // Assert
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once());
    }

    [Fact]
    public void OverloadedPeer_HoldsPauseUntilBelowResumeThreshold_ThenResumesAtOriginalSpeed()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        // Captured before pausing and restored on resume.
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Play_1x);

        var connections = serverComponent.Container.Resolve<ConnectionCollection>();
        var manager = serverComponent.Container.Resolve<IOverloadedPeerManager>();

        var peer = AddConnectedPeer(connections);

        // Overloaded -> pause.
        peer.SetQueueLength(AbovePauseThreshold);
        manager.CheckForOverloadedPeers();
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once());

        // Drained under the pause threshold but still above the resume threshold -> stay paused
        // (this is the hysteresis: no resume yet).
        peer.SetQueueLength(BetweenThresholds);
        manager.CheckForOverloadedPeers();
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_1x), Times.Never());

        // Drained below the resume threshold -> resume at the pre-pause speed.
        peer.SetQueueLength(BelowResumeThreshold);
        manager.CheckForOverloadedPeers();
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_1x), Times.Once());
    }

    private LiteNetLib.NetPeer AddConnectedPeer(ConnectionCollection connections)
    {
        var peer = TestNetwork.CreatePeer();
        connections.PlayerJoiningHandler(new MessagePayload<PlayerConnected>(this, new PlayerConnected(peer)));
        return peer;
    }
}
