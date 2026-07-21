using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Core.Server.Services.MobileParties;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using GameInterface.Services.MobileParties.Data;
using LiteNetLib;
using Moq;
using System;
using System.Linq;
using System.Threading;
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
        private readonly Mock<IJoinCampaignBaselineSender> baselineSender;

        public LoadingStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new TypedParameter(typeof(NetPeer), playerPeer));
            baselineSender = container.Resolve<Mock<IJoinCampaignBaselineSender>>();

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
            var state = connectionLogic.SetState<LoadingState>();
            StartReplay(state);
            Assert.Single(serverComponent.TestMessageBroker.GetMessagesFromType<PlayerCampaignEntered>());
            Assert.Equal(1, SignalCount(JoinSyncSignal.ReplayComplete));
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Never);

            SendAndDrain(state, JoinSyncSignal.CatchUpApplied);
            Assert.IsType<LoadingState>(connectionLogic.State);
            SendAndDrain(state, JoinSyncSignal.ReplayApplied);
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Once);

            Signal(state, JoinSyncSignal.BaselineApplied);
            Signal(state, JoinSyncSignal.FinalBaselineApplied);
            DrainGameThread();
            Assert.Equal(0, SignalCount(JoinSyncSignal.WorldReady));
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(3));
            SendAndDrain(state, JoinSyncSignal.BaselineApplied);
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(4));
            Assert.Equal(0, SignalCount(JoinSyncSignal.WorldReady));
            SendAndDrain(state, JoinSyncSignal.FinalBaselineApplied);
            Assert.Equal(1, SignalCount(JoinSyncSignal.WorldReady));
            Assert.IsType<LoadingState>(connectionLogic.State);
            SendAndDrain(state, JoinSyncSignal.CatchUpApplied);
            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_InvalidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();

            // Act
            var payload = new MessagePayload<NetworkPlayerCampaignEntered>(
                differentPeer, new NetworkPlayerCampaignEntered());
            currentState.PlayerCampaignEnteredHandler(payload);
            DrainGameThread();

            // Assert
            Assert.Empty(serverComponent.TestMessageBroker.GetMessagesFromType<PlayerCampaignEntered>());

            Assert.False(serverComponent.TestNetwork.SentNetworkMessages.ContainsKey(playerPeer.Id));
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void ReplayApplied_SendsBaselineAfterReplayMarker()
        {
            var state = connectionLogic.SetState<LoadingState>();
            baselineSender
                .Setup(sender => sender.Send(playerPeer))
                .Callback(() => serverComponent.TestNetwork.SendImmediate(
                    playerPeer,
                    new NetworkJoinCampaignBaseline(123L, Array.Empty<MobilePartyJoinState>())));
            StartReplay(state);
            var beforeAck = serverComponent.TestNetwork.GetPeerMessages(playerPeer).ToArray();
            Assert.Equal(
                JoinSyncSignal.ReplayComplete,
                Assert.IsType<NetworkJoinSync>(beforeAck.Last()).Signal);
            Assert.DoesNotContain(beforeAck, message => message is NetworkJoinCampaignBaseline);
            SendAndDrain(state, JoinSyncSignal.ReplayApplied);
            Assert.IsType<NetworkJoinCampaignBaseline>(
                serverComponent.TestNetwork.GetPeerMessages(playerPeer).Last());
        }

        [Fact]
        public void ReplayReplyDuringMarkerSend_IsAccepted()
        {
            var connectionLogicMock = new Mock<IConnectionLogic>();
            var networkMock = new Mock<INetwork>();
            var baselineSender = new Mock<IJoinCampaignBaselineSender>();
            connectionLogicMock.SetupGet(logic => logic.Peer).Returns(playerPeer);

            var state = new LoadingState(
                connectionLogicMock.Object,
                serverComponent.TestMessageBroker,
                networkMock.Object,
                baselineSender.Object,
                new Mock<IConnectionMessageQueue>().Object,
                new Mock<ISendCoalescer>().Object);
            connectionLogicMock.SetupGet(logic => logic.State).Returns(state);
            networkMock
                .Setup(network => network.SendImmediate(playerPeer, It.IsAny<IMessage>()))
                .Callback<NetPeer, IMessage>((_, message) =>
                {
                    if (message is NetworkJoinSync { Signal: JoinSyncSignal.ReplayComplete })
                        Signal(state, JoinSyncSignal.ReplayApplied);
                });
            CampaignEntered(state);
            DrainGameThread();
            DrainGameThread();
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Once);
            state.Dispose();
        }

        [Fact]
        public void BaselineRefresh_RequiresReplayAndIgnoresDuplicateQueuedRequest()
        {
            var state = connectionLogic.SetState<LoadingState>();
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            baselineSender.Verify(sender => sender.Send(It.IsAny<NetPeer>()), Times.Never);
            StartBaseline(state);
            Signal(state, JoinSyncSignal.BaselineRequested);
            Signal(state, JoinSyncSignal.BaselineRequested);
            DrainGameThread();
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(2));
        }

        [Fact]
        public void BaselineRefreshRequestDuringInitialBaselineSend_IsAccepted()
        {
            var state = connectionLogic.SetState<LoadingState>();
            int sendCount = 0;
            baselineSender
                .Setup(sender => sender.Send(playerPeer))
                .Callback(() =>
                {
                    if (++sendCount == 1)
                        Signal(state, JoinSyncSignal.BaselineRequested);
                });
            StartBaseline(state);
            DrainGameThread();
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(2));
        }

        [Fact]
        public void FinalAcknowledgementDuringFinalBaselineSend_OpensThenWaitsForCatchUp()
        {
            var state = connectionLogic.SetState<LoadingState>();
            int sendCount = 0;
            baselineSender
                .Setup(sender => sender.Send(playerPeer))
                .Callback(() =>
                {
                    if (++sendCount == 2)
                        Signal(state, JoinSyncSignal.BaselineApplied);
                    else if (sendCount == 3)
                        Signal(state, JoinSyncSignal.FinalBaselineApplied);
                });
            StartBaseline(state);
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            DrainGameThread();
            DrainGameThread();
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(3));
            Assert.Equal(1, SignalCount(JoinSyncSignal.WorldReady));
            Assert.IsType<LoadingState>(connectionLogic.State);
            SendAndDrain(state, JoinSyncSignal.CatchUpApplied);
            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Theory]
        [InlineData(JoinSyncSignal.BaselineRequested, 1, false)]
        [InlineData(JoinSyncSignal.ReplayApplied, 0, false)]
        [InlineData(JoinSyncSignal.CatchUpApplied, 3, true)]
        public void ReplacedLoadingState_DoesNotApplyQueuedSignal(
            JoinSyncSignal signal,
            int expectedBaselineSends,
            bool remainLoading)
        {
            var state = connectionLogic.SetState<LoadingState>();
            if (signal == JoinSyncSignal.ReplayApplied)
                StartReplay(state);
            else
                StartBaseline(state);
            if (signal == JoinSyncSignal.CatchUpApplied)
            {
                SendAndDrain(state, JoinSyncSignal.BaselineRequested);
                SendAndDrain(state, JoinSyncSignal.BaselineApplied);
                SendAndDrain(state, JoinSyncSignal.FinalBaselineApplied);
            }
            WhileGameThreadBlocked(() =>
            {
                Signal(state, signal);
                if (remainLoading)
                    connectionLogic.SetState<LoadingState>();
                else
                    connectionLogic.SetState<CampaignState>();
            });
            baselineSender.Verify(
                sender => sender.Send(playerPeer),
                Times.Exactly(expectedBaselineSends));
            Assert.Equal(remainLoading, connectionLogic.State is LoadingState);
        }

        private void StartBaseline(LoadingState state)
        {
            StartReplay(state);
            SendAndDrain(state, JoinSyncSignal.ReplayApplied);
        }

        private void StartReplay(LoadingState state)
        {
            CampaignEntered(state);
            DrainGameThread();
        }

        private void SendAndDrain(LoadingState state, JoinSyncSignal signal)
        {
            Signal(state, signal);
            DrainGameThread();
        }

        private void CampaignEntered(LoadingState state, NetPeer peer = null) =>
            state.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(
                    peer ?? playerPeer,
                    new NetworkPlayerCampaignEntered()));

        private void Signal(LoadingState state, JoinSyncSignal signal) =>
            state.JoinSyncHandler(
                new MessagePayload<NetworkJoinSync>(playerPeer, new NetworkJoinSync(signal)));

        private int SignalCount(JoinSyncSignal signal) =>
            serverComponent.TestNetwork
                .GetPeerMessagesFromType<NetworkJoinSync>(playerPeer)
                .Count(message => message.Signal == signal);

        private static void WhileGameThreadBlocked(Action action)
        {
            using var entered = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            GameThread.Run(() =>
            {
                entered.Set();
                release.Wait();
            });
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

            try
            {
                action();
            }
            finally
            {
                release.Set();
            }
            DrainGameThread();
        }

        private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
    }
}
