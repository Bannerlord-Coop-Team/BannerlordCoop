using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Core.Server.Services.MobileParties;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using LiteNetLib;
using Moq;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class LoadingStateTests
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        public LoadingStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new TypedParameter(typeof(NetPeer), playerPeer));

            differentPeer.SetId(playerPeer.Id + 1);
        }

        [Fact]
        public void EnterCampaignMethod_TransitionState_CampaignState()
        {
            // Arrange
            connectionLogic.SetState<LoadingState>();

            // Act
            connectionLogic.EnterCampaign();

            // Assert
            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.SetState<LoadingState>();

            // Act
            connectionLogic.CreateCharacter();
            connectionLogic.TransferSave();
            connectionLogic.Load();
            connectionLogic.EnterMission();

            // Assert
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void JoinHandshake_WaitsForReplayAndRefreshedBaselineAcknowledgements()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();
            var baselineSender = serverComponent.Container.Resolve<Mock<IJoinCampaignBaselineSender>>();

            // Act
            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(playerPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();

            // Assert
            Assert.Single(serverComponent.TestMessageBroker.GetMessagesFromType<PlayerCampaignEntered>());
            Assert.Single(serverComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinReplayComplete>(playerPeer));
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Never);

            currentState.JoinCatchUpAppliedHandler(
                new MessagePayload<NetworkJoinCatchUpApplied>(playerPeer, new NetworkJoinCatchUpApplied()));
            DrainGameThread();
            Assert.IsType<LoadingState>(connectionLogic.State);

            currentState.JoinReplayAppliedHandler(
                new MessagePayload<NetworkJoinReplayApplied>(playerPeer, new NetworkJoinReplayApplied()));
            DrainGameThread();
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Once);
            Assert.IsType<LoadingState>(connectionLogic.State);

            currentState.JoinCatchUpAppliedHandler(
                new MessagePayload<NetworkJoinCatchUpApplied>(playerPeer, new NetworkJoinCatchUpApplied()));
            DrainGameThread();
            Assert.IsType<LoadingState>(connectionLogic.State);

            currentState.JoinCampaignBaselineRequestedHandler(
                new MessagePayload<NetworkJoinCampaignBaselineRequested>(
                    playerPeer,
                    new NetworkJoinCampaignBaselineRequested()));
            DrainGameThread();
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(2));
            Assert.IsType<LoadingState>(connectionLogic.State);

            currentState.JoinCatchUpAppliedHandler(
                new MessagePayload<NetworkJoinCatchUpApplied>(playerPeer, new NetworkJoinCatchUpApplied()));
            DrainGameThread();

            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_InvalidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();

            // Act
            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(differentPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();

            // Assert
            Assert.Empty(serverComponent.TestMessageBroker.GetMessagesFromType<PlayerCampaignEntered>());
            Assert.False(serverComponent.TestNetwork.SentNetworkMessages.ContainsKey(playerPeer.Id));
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void ReplayApplied_SendsBaselineAfterReplayMarker()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();
            serverComponent.Container.Resolve<Mock<IJoinCampaignBaselineSender>>()
                .Setup(sender => sender.Send(playerPeer))
                .Callback(() => serverComponent.TestNetwork.SendImmediate(
                    playerPeer,
                    new NetworkJoinCampaignBaseline(123L, Array.Empty<MobilePartyPositionData>())));

            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(playerPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();

            // Assert
            var beforeAck = serverComponent.TestNetwork.GetPeerMessages(playerPeer).ToArray();
            Assert.IsType<NetworkJoinReplayComplete>(beforeAck.Last());
            Assert.DoesNotContain(beforeAck, message => message is NetworkJoinCampaignBaseline);

            // Act
            currentState.JoinReplayAppliedHandler(
                new MessagePayload<NetworkJoinReplayApplied>(playerPeer, new NetworkJoinReplayApplied()));
            DrainGameThread();

            // Assert
            var afterAck = serverComponent.TestNetwork.GetPeerMessages(playerPeer).ToArray();
            Assert.IsType<NetworkJoinCampaignBaseline>(afterAck.Last());
        }

        [Fact]
        public void ReplayReplyDuringMarkerSend_IsAccepted()
        {
            // Arrange
            var connectionLogicMock = new Mock<IConnectionLogic>();
            var networkMock = new Mock<INetwork>();
            var baselineSender = new Mock<IJoinCampaignBaselineSender>();
            connectionLogicMock.SetupGet(logic => logic.Peer).Returns(playerPeer);

            var currentState = new LoadingState(
                connectionLogicMock.Object,
                serverComponent.TestMessageBroker,
                networkMock.Object,
                baselineSender.Object);
            connectionLogicMock.SetupGet(logic => logic.State).Returns(currentState);
            networkMock
                .Setup(network => network.SendImmediate(playerPeer, It.IsAny<IMessage>()))
                .Callback<NetPeer, IMessage>((_, message) =>
                {
                    if (message is NetworkJoinReplayComplete)
                    {
                        currentState.JoinReplayAppliedHandler(
                            new MessagePayload<NetworkJoinReplayApplied>(
                                playerPeer,
                                new NetworkJoinReplayApplied()));
                    }
                });

            // Act
            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(playerPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();

            // Assert
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Once);
            currentState.Dispose();
        }

        [Fact]
        public void ReplayApplied_BeforeMarker_DoesNotSendBaseline()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();
            var baselineSender = serverComponent.Container.Resolve<Mock<IJoinCampaignBaselineSender>>();

            // Act
            currentState.JoinReplayAppliedHandler(
                new MessagePayload<NetworkJoinReplayApplied>(playerPeer, new NetworkJoinReplayApplied()));
            DrainGameThread();

            // Assert
            baselineSender.Verify(sender => sender.Send(It.IsAny<NetPeer>()), Times.Never);
        }

        [Fact]
        public void BaselineRefreshRequest_RequiresInitialBaselineAndIgnoresDuplicateQueuedRequest()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();
            var baselineSender = serverComponent.Container.Resolve<Mock<IJoinCampaignBaselineSender>>();

            // Act: an early request cannot skip the replay handshake.
            currentState.JoinCampaignBaselineRequestedHandler(
                new MessagePayload<NetworkJoinCampaignBaselineRequested>(
                    playerPeer,
                    new NetworkJoinCampaignBaselineRequested()));
            DrainGameThread();
            baselineSender.Verify(sender => sender.Send(It.IsAny<NetPeer>()), Times.Never);

            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(playerPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();
            currentState.JoinReplayAppliedHandler(
                new MessagePayload<NetworkJoinReplayApplied>(playerPeer, new NetworkJoinReplayApplied()));
            DrainGameThread();

            currentState.JoinCampaignBaselineRequestedHandler(
                new MessagePayload<NetworkJoinCampaignBaselineRequested>(
                    playerPeer,
                    new NetworkJoinCampaignBaselineRequested()));
            currentState.JoinCampaignBaselineRequestedHandler(
                new MessagePayload<NetworkJoinCampaignBaselineRequested>(
                    playerPeer,
                    new NetworkJoinCampaignBaselineRequested()));
            DrainGameThread();

            // Assert
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(2));
        }

        [Fact]
        public void BaselineRefreshRequestDuringInitialBaselineSend_IsAccepted()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();
            var baselineSender = serverComponent.Container.Resolve<Mock<IJoinCampaignBaselineSender>>();
            int sendCount = 0;
            baselineSender
                .Setup(sender => sender.Send(playerPeer))
                .Callback(() =>
                {
                    sendCount++;
                    if (sendCount == 1)
                    {
                        currentState.JoinCampaignBaselineRequestedHandler(
                            new MessagePayload<NetworkJoinCampaignBaselineRequested>(
                                playerPeer,
                                new NetworkJoinCampaignBaselineRequested()));
                    }
                });
            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(playerPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();

            // Act
            currentState.JoinReplayAppliedHandler(
                new MessagePayload<NetworkJoinReplayApplied>(playerPeer, new NetworkJoinReplayApplied()));
            DrainGameThread();

            // Assert
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(2));
        }

        [Fact]
        public void CatchUpReplyDuringRefreshedBaselineSend_IsAccepted()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();
            var baselineSender = serverComponent.Container.Resolve<Mock<IJoinCampaignBaselineSender>>();
            int sendCount = 0;
            baselineSender
                .Setup(sender => sender.Send(playerPeer))
                .Callback(() =>
                {
                    sendCount++;
                    if (sendCount == 2)
                    {
                        currentState.JoinCatchUpAppliedHandler(
                            new MessagePayload<NetworkJoinCatchUpApplied>(
                                playerPeer,
                                new NetworkJoinCatchUpApplied()));
                    }
                });
            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(playerPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();
            currentState.JoinReplayAppliedHandler(
                new MessagePayload<NetworkJoinReplayApplied>(playerPeer, new NetworkJoinReplayApplied()));
            DrainGameThread();

            // Act
            currentState.JoinCampaignBaselineRequestedHandler(
                new MessagePayload<NetworkJoinCampaignBaselineRequested>(
                    playerPeer,
                    new NetworkJoinCampaignBaselineRequested()));
            DrainGameThread();

            // Assert
            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void ReplacedLoadingState_DoesNotSendQueuedBaselineRefresh()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();
            var baselineSender = serverComponent.Container.Resolve<Mock<IJoinCampaignBaselineSender>>();
            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(playerPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();
            currentState.JoinReplayAppliedHandler(
                new MessagePayload<NetworkJoinReplayApplied>(playerPeer, new NetworkJoinReplayApplied()));
            DrainGameThread();

            // Act
            currentState.JoinCampaignBaselineRequestedHandler(
                new MessagePayload<NetworkJoinCampaignBaselineRequested>(
                    playerPeer,
                    new NetworkJoinCampaignBaselineRequested()));
            connectionLogic.SetState<CampaignState>();
            DrainGameThread();

            // Assert
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Once);
        }

        [Fact]
        public void ReplacedLoadingState_DoesNotSendQueuedBaseline()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();
            var baselineSender = serverComponent.Container.Resolve<Mock<IJoinCampaignBaselineSender>>();

            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(playerPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();

            // Act
            currentState.JoinReplayAppliedHandler(
                new MessagePayload<NetworkJoinReplayApplied>(playerPeer, new NetworkJoinReplayApplied()));
            connectionLogic.SetState<CampaignState>();
            DrainGameThread();

            // Assert
            baselineSender.Verify(sender => sender.Send(It.IsAny<NetPeer>()), Times.Never);
        }

        [Fact]
        public void ReplacedLoadingState_DoesNotApplyQueuedCatchUpAcknowledgement()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();
            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(playerPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();
            currentState.JoinReplayAppliedHandler(
                new MessagePayload<NetworkJoinReplayApplied>(playerPeer, new NetworkJoinReplayApplied()));
            DrainGameThread();
            currentState.JoinCampaignBaselineRequestedHandler(
                new MessagePayload<NetworkJoinCampaignBaselineRequested>(
                    playerPeer,
                    new NetworkJoinCampaignBaselineRequested()));
            DrainGameThread();

            // Act
            currentState.JoinCatchUpAppliedHandler(
                new MessagePayload<NetworkJoinCatchUpApplied>(playerPeer, new NetworkJoinCatchUpApplied()));
            connectionLogic.SetState<LoadingState>();
            DrainGameThread();

            // Assert
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
    }
}
