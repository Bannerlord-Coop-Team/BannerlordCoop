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
}
