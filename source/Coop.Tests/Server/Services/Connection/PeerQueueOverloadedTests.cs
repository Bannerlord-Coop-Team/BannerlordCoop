using Autofac;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Connection.Handlers;
using Coop.Core.Server.Services.Time.Messages;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using System;
using System.Linq;
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
        /// Create a new peer on the test network
        var netPeer = TestNetwork.CreatePeer();
        /// Set peer queue length greater than 0 so <see cref="PeerQueueOverloadedHandler.Poll"/> does not resume the game
        netPeer.SetQueueLength(1);

        // Act
        /// This is handled by <see cref="PeerQueueOverloadedHandler.Handle"/>
        TestMessageBroker.Publish(this, new PeerQueueOverloaded(netPeer));

        // Assert
        /// 1 of each pause message is sent
        Assert.Equal(1, TestMessageBroker.GetMessageCountFromType<SetTimeControlMode>());
        Assert.Equal(1, TestNetwork.GetPeerMessageCountFromType<NetworkChangeTimeControlMode>(netPeer));

        /// Gets the last internal and network message of their respected types for asserting below
        var internalMsg = TestMessageBroker.GetMessagesFromType<SetTimeControlMode>().Last();
        var networkMsg = TestNetwork.GetPeerMessagesFromType<NetworkChangeTimeControlMode>(netPeer).Last();

        /// Game is commanded to pause internally and throughout the network
        Assert.Equal(TimeControlEnum.Pause, internalMsg.NewTimeMode);
        Assert.Equal(TimeControlEnum.Pause, networkMsg.NewControlMode);
    }

    [Fact]
    public void ClientsCatchUp_Publishes_ResumeMessages()
    {
        // Arrange
        var peerOverloadedHandler = serverComponent.Container.Resolve<PeerQueueOverloadedHandler>();

        var netPeer = TestNetwork.CreatePeer();

        /// Set peer queue length greater than 0 so <see cref="PeerQueueOverloadedHandler.Poll"/> does not resume the game
        netPeer.SetQueueLength(1);
        /// This is handled by <see cref="PeerQueueOverloadedHandler.Handle"/>
        TestMessageBroker.Publish(this, new PeerQueueOverloaded(netPeer));
        /// Set peer queue length to 0 so <see cref="PeerQueueOverloadedHandler.Poll"/> resumes the game
        netPeer.SetQueueLength(0);

        // Act
        /// Trigger polling manually to always ensure polling happens instead of waiting
        peerOverloadedHandler.Poll(TimeSpan.Zero);

        // Assert
        /// When the game resumes, a resume message is sent internally and over the network
        /// 2 messages exist because the server forces a pause before resuming
        Assert.Equal(2, TestMessageBroker.GetMessageCountFromType<SetTimeControlMode>());
        Assert.Equal(2, TestNetwork.GetPeerMessageCountFromType<NetworkChangeTimeControlMode>(netPeer));

        var internalMsg = TestMessageBroker.GetMessagesFromType<SetTimeControlMode>().Last();
        var networkMsg = TestNetwork.GetPeerMessagesFromType<NetworkChangeTimeControlMode>(netPeer).Last();

        /// Resume value defaults to <see cref="TimeControlEnum.Play_1x"/>
        Assert.Equal(TimeControlEnum.Play_1x, internalMsg.NewTimeMode);
        Assert.Equal(TimeControlEnum.Play_1x, networkMsg.NewControlMode);
    }
}
