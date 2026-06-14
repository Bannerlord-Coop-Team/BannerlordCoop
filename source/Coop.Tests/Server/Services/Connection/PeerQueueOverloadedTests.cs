using Autofac;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Connection.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using Moq;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Services.Connection;

public class PeerQueueOverloadedTests
{
    private readonly ServerTestComponent serverComponent;

    private TestMessageBroker TestMessageBroker => serverComponent.TestMessageBroker;
    private TestNetwork TestNetwork => serverComponent.TestNetwork;

    public PeerQueueOverloadedTests(ITestOutputHelper output)
    {
        serverComponent = new ServerTestComponent(output);
    }

    [Fact]
    public void PeerQueueOverloadedReceived_Publishes_SetTimeControlMode()
    {
        // Arrange
        /// Time control (and the NetworkChangeTimeControlMode broadcast) now lives behind ITimeControlInterface,
        /// which is mocked in tests; assert the handler drives it rather than sending the network message itself.
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();

        /// Create a new peer on the test network
        var netPeer = TestNetwork.CreatePeer();
        /// Set peer queue length greater than 0 so <see cref="PeerQueueOverloadedHandler.Poll"/> does not resume the game
        netPeer.SetQueueLength(1);

        // Act
        /// This is handled by <see cref="PeerQueueOverloadedHandler.Handle"/>
        TestMessageBroker.Publish(this, new PeerQueueOverloaded(netPeer));

        // Assert
        /// An overloaded peer pauses the game exactly once
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once());
    }

    [Fact]
    public void ClientsCatchUp_Publishes_ResumeMessages()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        /// The handler captures the current speed (GetTimeControl) before pausing and restores it on resume.
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Play_1x);

        var peerOverloadedHandler = serverComponent.Container.Resolve<PeerQueueOverloadedHandler>();

        var netPeer = TestNetwork.CreatePeer();

        /// Set peer queue length greater than 0 so <see cref="PeerQueueOverloadedHandler.Poll"/> does not resume the game
        netPeer.SetQueueLength(1);
        /// This is handled by <see cref="PeerQueueOverloadedHandler.Handle"/>
        TestMessageBroker.Publish(this, new PeerQueueOverloaded(netPeer));
        /// Prevent polling
        peerOverloadedHandler.Poller.Stop();
        /// Set peer queue length to 0 so <see cref="PeerQueueOverloadedHandler.Poll"/> resumes the game
        netPeer.SetQueueLength(0);

        // Act
        /// Trigger polling manually to always ensure polling happens instead of waiting
        peerOverloadedHandler.Poll(TimeSpan.Zero);

        // Assert
        /// The server forces a pause when overloaded, then resumes at the pre-pause speed once the queue clears.
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once());
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_1x), Times.Once());
    }

    [Fact]
    public void PeerQueueCongestedReceived_LimitsTimeTo1x()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        /// Players were fast-forwarding; a congested client caps the speed at 1x instead of pausing.
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Play_2x);

        var netPeer = TestNetwork.CreatePeer();
        /// Queue length greater than 0 so <see cref="PeerQueueOverloadedHandler.Poll"/> does not resume the game
        netPeer.SetQueueLength(1);
        netPeer.SetConnected();

        // Act
        /// This is handled by <see cref="PeerQueueOverloadedHandler.Handle_PeerQueueCongested"/>
        TestMessageBroker.Publish(this, new PeerQueueCongested(netPeer));

        // Assert
        /// The moderate tier slows time to 1x; it does not pause.
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_1x), Times.Once());
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Never());
    }

    [Fact]
    public void PeerQueueCongested_WhileGamePaused_DoesNotUnpause()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        /// The players had paused the game; the slow-down tier must never speed it up to 1x.
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Pause);

        var netPeer = TestNetwork.CreatePeer();
        netPeer.SetQueueLength(1);
        netPeer.SetConnected();

        // Act
        TestMessageBroker.Publish(this, new PeerQueueCongested(netPeer));

        // Assert
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_1x), Times.Never());
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once());
    }

    [Fact]
    public void EscalatingPeer_CapturesOriginalSpeedOnce_AndRestoresIt()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Play_2x);

        var handler = serverComponent.Container.Resolve<PeerQueueOverloadedHandler>();

        var netPeer = TestNetwork.CreatePeer();
        netPeer.SetQueueLength(1);
        netPeer.SetConnected();

        // Act
        /// The same client crosses the slow-down threshold, then the pause threshold.
        TestMessageBroker.Publish(this, new PeerQueueCongested(netPeer));
        TestMessageBroker.Publish(this, new PeerQueueOverloaded(netPeer));
        handler.Poller.Stop();

        // Assert
        /// The pre-throttle speed is captured exactly once, not re-read after it has already been lowered.
        timeControlMock.Verify(t => t.GetTimeControl(), Times.Once());

        // Act
        /// Client catches up.
        netPeer.SetQueueLength(0);
        handler.Poll(TimeSpan.Zero);

        // Assert
        /// Slowed to 1x, then paused, then restored to the players' original 2x (never left at 1x).
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_1x), Times.Once());
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once());
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_2x), Times.Once());
    }

    [Fact]
    public void OverloadedPeerCatchesUp_WhileCongestedRemains_StepsDownTo1x()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Play_2x);

        var handler = serverComponent.Container.Resolve<PeerQueueOverloadedHandler>();

        var overloadedPeer = TestNetwork.CreatePeer();
        overloadedPeer.SetQueueLength(1);
        overloadedPeer.SetConnected();
        var congestedPeer = TestNetwork.CreatePeer("127.0.0.2");
        congestedPeer.SetQueueLength(1);
        congestedPeer.SetConnected();

        // Act
        TestMessageBroker.Publish(this, new PeerQueueOverloaded(overloadedPeer));
        TestMessageBroker.Publish(this, new PeerQueueCongested(congestedPeer));
        handler.Poller.Stop();

        /// The overloaded client catches up; the congested one is still behind.
        overloadedPeer.SetQueueLength(0);
        handler.Poll(TimeSpan.Zero);

        // Assert
        /// Time steps down from pause to 1x rather than fully resuming while a client is still catching up.
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Pause), Times.Once());
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_1x), Times.Once());
        timeControlMock.Verify(t => t.ServerSetTimeControl(TimeControlEnum.Play_2x), Times.Never());
    }

    [Fact]
    public void Policies_ReflectCatchUpState()
    {
        // Arrange
        var timeControlMock = serverComponent.Container.Resolve<Mock<ITimeControlInterface>>();
        timeControlMock.Setup(t => t.GetTimeControl()).Returns(TimeControlEnum.Play_2x);

        var handler = serverComponent.Container.Resolve<PeerQueueOverloadedHandler>();

        // Assert
        /// Nothing catching up: both actions allowed.
        Assert.True(handler.PausePolicy());
        Assert.True(handler.SlowDownPolicy());

        // Act
        var congestedPeer = TestNetwork.CreatePeer();
        congestedPeer.SetQueueLength(1);
        congestedPeer.SetConnected();
        TestMessageBroker.Publish(this, new PeerQueueCongested(congestedPeer));
        handler.Poller.Stop();

        // Assert
        /// Congested: fast-forward blocked, unpause still allowed.
        Assert.True(handler.PausePolicy());
        Assert.False(handler.SlowDownPolicy());

        // Act
        var overloadedPeer = TestNetwork.CreatePeer("127.0.0.2");
        overloadedPeer.SetQueueLength(1);
        overloadedPeer.SetConnected();
        TestMessageBroker.Publish(this, new PeerQueueOverloaded(overloadedPeer));
        handler.Poller.Stop();

        // Assert
        /// Overloaded: both blocked.
        Assert.False(handler.PausePolicy());
        Assert.False(handler.SlowDownPolicy());
    }
}
