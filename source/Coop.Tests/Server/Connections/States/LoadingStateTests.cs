using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Network.Coalescing;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Core.Server.Services.MobileParties;
using Coop.Core.Server.Services.Kingdoms;
using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.Players;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly Mock<IJoinCampaignKingdomBaseLineSender> kingdomBaselineSender;
        private readonly IPlayerManager playerManager;

        public LoadingStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new TypedParameter(typeof(NetPeer), playerPeer));
            container.Resolve<IConnectionMessageQueue>().BeginQueueing(playerPeer);
            baselineSender = container.Resolve<Mock<IJoinCampaignBaselineSender>>();
            kingdomBaselineSender = container.Resolve<Mock<IJoinCampaignKingdomBaseLineSender>>();
            playerManager = container.Resolve<IPlayerManager>();

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
            Assert.Single(serverComponent.TestMessageBroker.GetMessagesFromType<PlayerConnectionStateChanged>());
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
            Assert.Empty(serverComponent.TestMessageBroker.GetMessagesFromType<PlayerConnectionStateChanged>());

            Assert.False(serverComponent.TestNetwork.SentNetworkMessages.ContainsKey(playerPeer.Id));
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignEntered_ReusedPeerIdentityCannotAdvanceOldConnection()
        {
            var currentState = connectionLogic.SetState<LoadingState>();
            var replacementPeer = serverComponent.TestNetwork.CreatePeer();
            replacementPeer.SetId(playerPeer.Id);
            Assert.NotSame(playerPeer, replacementPeer);
            Assert.Equal(playerPeer, replacementPeer);

            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(
                    replacementPeer,
                    new NetworkPlayerCampaignEntered()));
            DrainGameThread();

            Assert.True(currentState.IsPreEntryPending);
            Assert.Empty(serverComponent.TestMessageBroker.GetMessagesFromType<PlayerCampaignEntered>());
        }

        [Fact]
        public void JoinSync_ReusedPeerIdentityCannotAdvanceOldConnection()
        {
            var currentState = connectionLogic.SetState<LoadingState>();
            StartReplay(currentState);
            var replacementPeer = serverComponent.TestNetwork.CreatePeer();
            replacementPeer.SetId(playerPeer.Id);
            Assert.NotSame(playerPeer, replacementPeer);
            Assert.Equal(playerPeer, replacementPeer);

            currentState.JoinSyncHandler(
                new MessagePayload<NetworkJoinSync>(
                    replacementPeer,
                    new NetworkJoinSync(JoinSyncSignal.ReplayApplied)));
            DrainGameThread();

            Assert.True(currentState.IsWaitingForReplayApplied);
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Never);
        }

        [Fact]
        public void ReplayApplied_SendsBaselineAfterReplayMarker()
        {
            var state = connectionLogic.SetState<LoadingState>();
            baselineSender
                .Setup(sender => sender.Send(playerPeer))
                .Callback(() => serverComponent.TestNetwork.SendImmediate(
                    playerPeer,
                    new NetworkJoinCampaignBaseline(
                        123L,
                        TimeControlEnum.Play_1x,
                        Array.Empty<MobilePartyJoinState>())));
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
        public void OversizedReplayBatch_WaitsForMatchingClientApplicationAcknowledgement()
        {
            var connectionLogicMock = new Mock<IConnectionLogic>();
            var networkMock = new Mock<INetwork>();
            var queueMock = new Mock<IConnectionMessageQueue>();
            var coalescerMock = new Mock<ISendCoalescer>();
            var sentSignals = new List<NetworkJoinSync>();
            connectionLogicMock.SetupGet(logic => logic.Peer).Returns(playerPeer);
            queueMock
                .SetupSequence(queue => queue.FlushBatch(playerPeer))
                .Returns(new JoinReplayBatchResult(1, NetworkJoinLimits.MaxReplayBatchBytes + 1, hasMore: true))
                .Returns(new JoinReplayBatchResult(5, 500, hasMore: false));
            networkMock
                .Setup(network => network.SendImmediate(playerPeer, It.IsAny<IMessage>()))
                .Callback<NetPeer, IMessage>((_, message) =>
                {
                    if (message is NetworkJoinSync signal) sentSignals.Add(signal);
                });

            var state = new LoadingState(
                connectionLogicMock.Object,
                serverComponent.TestMessageBroker,
                networkMock.Object,
                baselineSender.Object,
                kingdomBaselineSender.Object,
                queueMock.Object,
                coalescerMock.Object,
                playerManager);
            connectionLogicMock.SetupGet(logic => logic.State).Returns(state);

            CampaignEntered(state);
            DrainGameThread();

            NetworkJoinSync firstBatch = Assert.Single(
                sentSignals,
                signal => signal.Signal == JoinSyncSignal.ReplayBatchComplete);
            Assert.True(firstBatch.ReplayBatchId > 0);
            Assert.DoesNotContain(
                sentSignals,
                signal => signal.Signal == JoinSyncSignal.ReplayComplete);
            long progressBeforeAcknowledgement = state.JoinProgressVersion;

            Signal(state, JoinSyncSignal.ReplayBatchApplied, firstBatch.ReplayBatchId + 1);
            DrainGameThread();
            queueMock.Verify(queue => queue.FlushBatch(playerPeer), Times.Once);
            Assert.Equal(progressBeforeAcknowledgement, state.JoinProgressVersion);

            Signal(state, JoinSyncSignal.ReplayBatchApplied, firstBatch.ReplayBatchId);
            DrainGameThread();

            queueMock.Verify(queue => queue.FlushBatch(playerPeer), Times.Exactly(2));
            Assert.Equal(progressBeforeAcknowledgement + 1, state.JoinProgressVersion);
            Assert.Single(sentSignals, signal => signal.Signal == JoinSyncSignal.ReplayComplete);

            Signal(state, JoinSyncSignal.ReplayBatchApplied, firstBatch.ReplayBatchId);
            DrainGameThread();
            queueMock.Verify(queue => queue.FlushBatch(playerPeer), Times.Exactly(2));
            Assert.Equal(progressBeforeAcknowledgement + 1, state.JoinProgressVersion);
            state.Dispose();
        }

        [Fact]
        public void ReplayBatchReplyDuringMarkerSend_DoesNotContinueRecursively()
        {
            var connectionLogicMock = new Mock<IConnectionLogic>();
            var networkMock = new Mock<INetwork>();
            var queueMock = new Mock<IConnectionMessageQueue>();
            var coalescerMock = new Mock<ISendCoalescer>();
            int flushCalls = 0;
            connectionLogicMock.SetupGet(logic => logic.Peer).Returns(playerPeer);
            queueMock
                .SetupSequence(queue => queue.FlushBatch(playerPeer))
                .Returns(() =>
                {
                    flushCalls++;
                    return new JoinReplayBatchResult(10, 1000, hasMore: true);
                })
                .Returns(() =>
                {
                    flushCalls++;
                    return new JoinReplayBatchResult(5, 500, hasMore: false);
                });

            LoadingState state = null!;
            networkMock
                .Setup(network => network.SendImmediate(playerPeer, It.IsAny<IMessage>()))
                .Callback<NetPeer, IMessage>((_, message) =>
                {
                    if (message is NetworkJoinSync
                        {
                            Signal: JoinSyncSignal.ReplayBatchComplete,
                        } batch)
                    {
                        Signal(state, JoinSyncSignal.ReplayBatchApplied, batch.ReplayBatchId);
                        Assert.Equal(1, flushCalls);
                    }
                });
            state = new LoadingState(
                connectionLogicMock.Object,
                serverComponent.TestMessageBroker,
                networkMock.Object,
                baselineSender.Object,
                kingdomBaselineSender.Object,
                queueMock.Object,
                coalescerMock.Object,
                playerManager);
            connectionLogicMock.SetupGet(logic => logic.State).Returns(state);

            CampaignEntered(state);
            DrainGameThread();

            DrainGameThread();
            Assert.Equal(2, flushCalls);
            networkMock.Verify(
                network => network.SendImmediate(
                    playerPeer,
                    It.Is<NetworkJoinSync>(message => message.Signal == JoinSyncSignal.ReplayComplete)),
                Times.Once);
            state.Dispose();
        }

        [Fact]
        public void ReplayBatches_GateFinalBaselineAndWorldReadyContinuations()
        {
            var connectionLogicMock = new Mock<IConnectionLogic>();
            var networkMock = new Mock<INetwork>();
            var queueMock = new Mock<IConnectionMessageQueue>();
            var coalescerMock = new Mock<ISendCoalescer>();
            var senderMock = new Mock<IJoinCampaignBaselineSender>();
            var sentSignals = new List<NetworkJoinSync>();
            int openCalls = 0;
            connectionLogicMock.SetupGet(logic => logic.Peer).Returns(playerPeer);
            queueMock
                .SetupSequence(queue => queue.FlushBatch(playerPeer))
                .Returns(default(JoinReplayBatchResult)) // initial replay
                .Returns(default(JoinReplayBatchResult)) // first baseline
                .Returns(default(JoinReplayBatchResult)) // refreshed baseline
                .Returns(new JoinReplayBatchResult(1, NetworkJoinLimits.MaxReplayBatchBytes + 1, hasMore: true))
                .Returns(default(JoinReplayBatchResult));
            queueMock
                .Setup(queue => queue.OpenWithTailBatch(
                    playerPeer,
                    It.IsAny<IMessage>(),
                    It.IsAny<Func<bool>>()))
                .Returns((NetPeer _, IMessage marker, Func<bool> tryBeginOpen) =>
                {
                    if (++openCalls == 1)
                        return new JoinReplayBatchResult(1, NetworkJoinLimits.MaxReplayBatchBytes + 1, hasMore: true);
                    if (tryBeginOpen()) networkMock.Object.SendImmediate(playerPeer, marker);
                    return default;
                });
            networkMock
                .Setup(network => network.SendImmediate(playerPeer, It.IsAny<IMessage>()))
                .Callback<NetPeer, IMessage>((_, message) =>
                {
                    if (message is NetworkJoinSync signal) sentSignals.Add(signal);
                });
            var state = new LoadingState(
                connectionLogicMock.Object,
                serverComponent.TestMessageBroker,
                networkMock.Object,
                senderMock.Object,
                kingdomBaselineSender.Object,
                queueMock.Object,
                coalescerMock.Object,
                playerManager);
            connectionLogicMock.SetupGet(logic => logic.State).Returns(state);

            CampaignEntered(state);
            DrainGameThread();
            SendAndDrain(state, JoinSyncSignal.ReplayApplied);
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            SendAndDrain(state, JoinSyncSignal.BaselineApplied);
            Assert.Equal(2, senderMock.Invocations.Count);
            NetworkJoinSync finalBaselineBatch = sentSignals.Last(
                signal => signal.Signal == JoinSyncSignal.ReplayBatchComplete);

            Signal(
                state,
                JoinSyncSignal.ReplayBatchApplied,
                finalBaselineBatch.ReplayBatchId);
            DrainGameThread();

            senderMock.Verify(sender => sender.Send(playerPeer), Times.Exactly(3));
            queueMock.Verify(queue => queue.EndFinalBaselineCoverage(playerPeer), Times.Once);
            SendAndDrain(state, JoinSyncSignal.FinalBaselineApplied);
            Assert.DoesNotContain(
                sentSignals,
                signal => signal.Signal == JoinSyncSignal.WorldReady);
            NetworkJoinSync worldReadyBatch = sentSignals
                .Where(signal => signal.Signal == JoinSyncSignal.ReplayBatchComplete)
                .Last();
            Assert.NotEqual(finalBaselineBatch.ReplayBatchId, worldReadyBatch.ReplayBatchId);

            Signal(state, JoinSyncSignal.ReplayBatchApplied, worldReadyBatch.ReplayBatchId);
            DrainGameThread();

            Assert.Contains(sentSignals, signal => signal.Signal == JoinSyncSignal.WorldReady);
            queueMock.Verify(queue => queue.OpenWithTailBatch(
                playerPeer,
                It.IsAny<IMessage>(),
                It.IsAny<Func<bool>>()), Times.Exactly(2));
            state.Dispose();
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
                kingdomBaselineSender.Object,
                new Mock<IConnectionMessageQueue>().Object,
                new Mock<ISendCoalescer>().Object,
                playerManager);
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
        public void FinalBaseline_EndsCoverageAfterReplayAndImmediatelyBeforeCapture()
        {
            var connectionLogicMock = new Mock<IConnectionLogic>();
            var networkMock = new Mock<INetwork>();
            var queueMock = new Mock<IConnectionMessageQueue>();
            var coalescerMock = new Mock<ISendCoalescer>();
            var senderMock = new Mock<IJoinCampaignBaselineSender>();
            var calls = new List<string>();
            connectionLogicMock.SetupGet(logic => logic.Peer).Returns(playerPeer);
            queueMock
                .Setup(queue => queue.EndFinalBaselineCoverage(playerPeer))
                .Callback(() => calls.Add("cut"));
            queueMock
                .Setup(queue => queue.FlushBatch(playerPeer))
                .Callback(() => calls.Add("flush"))
                .Returns(default(JoinReplayBatchResult));
            senderMock
                .Setup(sender => sender.Send(playerPeer))
                .Callback(() => calls.Add("send"));

            var state = new LoadingState(
                connectionLogicMock.Object,
                serverComponent.TestMessageBroker,
                networkMock.Object,
                senderMock.Object,
                kingdomBaselineSender.Object,
                queueMock.Object,
                coalescerMock.Object,
                playerManager);
            connectionLogicMock.SetupGet(logic => logic.State).Returns(state);

            StartBaseline(state);
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            queueMock.Verify(
                queue => queue.EndFinalBaselineCoverage(playerPeer),
                Times.Never);

            calls.Clear();
            SendAndDrain(state, JoinSyncSignal.BaselineApplied);

            Assert.Equal(new[] { "flush", "cut", "send" }, calls);
            queueMock.Verify(
                queue => queue.EndFinalBaselineCoverage(playerPeer),
                Times.Once);
            state.Dispose();
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
        public void BaselineRequestLimit_BoundsFlushAndSendWorkForBrokenClients()
        {
            var connectionLogicMock = new Mock<IConnectionLogic>();
            var networkMock = new Mock<INetwork>();
            var queueMock = new Mock<IConnectionMessageQueue>();
            var coalescerMock = new Mock<ISendCoalescer>();
            var senderMock = new Mock<IJoinCampaignBaselineSender>();
            connectionLogicMock.SetupGet(logic => logic.Peer).Returns(playerPeer);
            var state = new LoadingState(
                connectionLogicMock.Object,
                serverComponent.TestMessageBroker,
                networkMock.Object,
                senderMock.Object,
                kingdomBaselineSender.Object,
                queueMock.Object,
                coalescerMock.Object,
                playerManager);
            connectionLogicMock.SetupGet(logic => logic.State).Returns(state);

            StartBaseline(state);
            for (int sent = 1; sent < LoadingState.MaxBaselinesPerJoin; sent++)
            {
                SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            }

            senderMock.Verify(
                sender => sender.Send(playerPeer),
                Times.Exactly(LoadingState.MaxBaselinesPerJoin));
            coalescerMock.Verify(
                coalescer => coalescer.Flush(networkMock.Object),
                Times.Exactly(LoadingState.MaxBaselinesPerJoin + 1));
            queueMock.Verify(
                queue => queue.FlushBatch(playerPeer),
                Times.Exactly(LoadingState.MaxBaselinesPerJoin + 1));

            SendAndDrain(state, JoinSyncSignal.BaselineRequested);

            senderMock.Verify(
                sender => sender.Send(playerPeer),
                Times.Exactly(LoadingState.MaxBaselinesPerJoin));
            coalescerMock.Verify(
                coalescer => coalescer.Flush(networkMock.Object),
                Times.Exactly(LoadingState.MaxBaselinesPerJoin + 1));
            queueMock.Verify(
                queue => queue.FlushBatch(playerPeer),
                Times.Exactly(LoadingState.MaxBaselinesPerJoin + 1));
            Assert.True(state.IsJoinCatchUpPending);
            state.Dispose();
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

        [Fact]
        public void FinalReplayOverflow_DoesNotOpenOrEmitWorldReady()
        {
            var connectionLogicMock = new Mock<IConnectionLogic>();
            var networkMock = new Mock<INetwork>();
            var queueMock = new Mock<IConnectionMessageQueue>();
            var coalescerMock = new Mock<ISendCoalescer>();
            var senderMock = new Mock<IJoinCampaignBaselineSender>();
            connectionLogicMock.SetupGet(logic => logic.Peer).Returns(playerPeer);
            queueMock
                .Setup(queue => queue.OpenWithTailBatch(
                    playerPeer,
                    It.IsAny<IMessage>(),
                    It.IsAny<Func<bool>>()))
                .Returns(new JoinReplayBatchResult(
                    packetsSent: 0,
                    bytesSent: 0,
                    hasMore: false,
                    overflowed: true));
            var state = new LoadingState(
                connectionLogicMock.Object,
                serverComponent.TestMessageBroker,
                networkMock.Object,
                senderMock.Object,
                kingdomBaselineSender.Object,
                queueMock.Object,
                coalescerMock.Object,
                playerManager);
            connectionLogicMock.SetupGet(logic => logic.State).Returns(state);

            StartBaseline(state);
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            SendAndDrain(state, JoinSyncSignal.BaselineApplied);
            SendAndDrain(state, JoinSyncSignal.FinalBaselineApplied);

            networkMock.Verify(
                network => network.SendImmediate(
                    playerPeer,
                    It.Is<NetworkJoinSync>(message => message.Signal == JoinSyncSignal.WorldReady)),
                Times.Never);
            connectionLogicMock.Verify(logic => logic.EnterCampaign(), Times.Never);
            Assert.True(state.IsJoinCatchUpPending);
            state.Dispose();
        }

        [Fact]
        public void PlayerCampaignSynchronized_IsPublishedOnlyAtTheTerminalJoinPhase()
        {
            // The trap this guards: the message named PlayerCampaignEntered is published at CampaignEntryQueued,
            // the SECOND of eleven join phases — before the replay flush and both baselines. Anything that needs the
            // peer's world to be consistent (resuming a battle it dropped out of) must key off the terminal phase
            // instead, or it fires five phases early against an unreplicated world.
            var completions = new List<NetPeer>();
            serverComponent.TestMessageBroker.Subscribe<PlayerCampaignSynchronized>(
                payload => completions.Add(payload.What.PlayerId));

            var state = connectionLogic.SetState<LoadingState>();

            CampaignEntered(state);
            DrainGameThread();
            Assert.Empty(completions);

            SendAndDrain(state, JoinSyncSignal.ReplayApplied);
            Assert.Empty(completions);

            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            Assert.Empty(completions);

            SendAndDrain(state, JoinSyncSignal.BaselineApplied);
            Assert.Empty(completions);

            SendAndDrain(state, JoinSyncSignal.FinalBaselineApplied);
            Assert.Empty(completions);

            SendAndDrain(state, JoinSyncSignal.CatchUpApplied);

            Assert.Equal(new[] { playerPeer }, completions);
            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void PlayerCampaignSynchronized_IsNotPublishedWhenTheJoinIsAbandoned()
        {
            var completions = new List<NetPeer>();
            serverComponent.TestMessageBroker.Subscribe<PlayerCampaignSynchronized>(
                payload => completions.Add(payload.What.PlayerId));

            var state = connectionLogic.SetState<LoadingState>();
            StartReplay(state);
            SendAndDrain(state, JoinSyncSignal.ReplayApplied);
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            SendAndDrain(state, JoinSyncSignal.BaselineApplied);
            SendAndDrain(state, JoinSyncSignal.FinalBaselineApplied);

            // The peer drops out of the loading state before the terminal phase applies.
            connectionLogic.SetState<CampaignState>();
            SendAndDrain(state, JoinSyncSignal.CatchUpApplied);

            Assert.Empty(completions);
        }

        [Fact]
        public void JoinCatchUpPending_StartsAfterReplayAndEndsWithCampaignEntry()
        {
            var state = connectionLogic.SetState<LoadingState>();
            Assert.False(state.IsJoinCatchUpPending);

            CampaignEntered(state);
            Assert.False(state.IsJoinCatchUpPending);
            DrainGameThread();
            Assert.True(state.IsJoinCatchUpPending);

            Signal(state, JoinSyncSignal.ReplayApplied);
            Assert.True(state.IsJoinCatchUpPending);
            DrainGameThread();

            Signal(state, JoinSyncSignal.BaselineRequested);
            Assert.True(state.IsJoinCatchUpPending);
            DrainGameThread();

            SendAndDrain(state, JoinSyncSignal.BaselineApplied);
            Assert.True(state.IsJoinCatchUpPending);

            SendAndDrain(state, JoinSyncSignal.FinalBaselineApplied);
            Assert.True(state.IsJoinCatchUpPending);

            SendAndDrain(state, JoinSyncSignal.CatchUpApplied);
            Assert.False(state.IsJoinCatchUpPending);
            Assert.IsType<CampaignState>(connectionLogic.State);
        }

        [Fact]
        public void DisconnectedLoadingState_DoesNotApplyQueuedCampaignEntry()
        {
            var state = connectionLogic.SetState<LoadingState>();
            StartBaseline(state);
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            SendAndDrain(state, JoinSyncSignal.BaselineApplied);
            SendAndDrain(state, JoinSyncSignal.FinalBaselineApplied);

            WhileGameThreadBlocked(() =>
            {
                Signal(state, JoinSyncSignal.CatchUpApplied);
                connectionLogic.Dispose();
            });

            Assert.Null(connectionLogic.State);
        }

        [Fact]
        public void AbortedJoin_DoesNotSendQueuedBaseline()
        {
            var state = connectionLogic.SetState<LoadingState>();
            StartReplay(state);

            WhileGameThreadBlocked(() =>
            {
                Signal(state, JoinSyncSignal.ReplayApplied);
                Assert.True(state.TryAbortJoinCatchUp());
            });

            Assert.True(state.IsAborted);
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Never);
        }

        [Fact]
        public void PreEntryAbort_CannotAbortAfterReplayPhaseStarts()
        {
            var state = connectionLogic.SetState<LoadingState>();

            Assert.True(state.IsPreEntryPending);
            StartReplay(state);

            Assert.False(state.IsPreEntryPending);
            Assert.True(state.IsWaitingForReplayApplied);
            Assert.False(state.TryAbortPreEntryJoin());
            Assert.False(state.IsAborted);
        }

        [Fact]
        public void AbortedJoin_DoesNotOpenQueuedWorldTail()
        {
            var state = connectionLogic.SetState<LoadingState>();
            StartBaseline(state);
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            SendAndDrain(state, JoinSyncSignal.BaselineApplied);

            WhileGameThreadBlocked(() =>
            {
                Signal(state, JoinSyncSignal.FinalBaselineApplied);
                Assert.True(state.TryAbortJoinCatchUp());
            });

            Assert.True(state.IsAborted);
            Assert.Equal(0, SignalCount(JoinSyncSignal.WorldReady));
        }

        [Fact]
        public async Task WorldTailOpen_AndConcurrentAbortAreSerialized()
        {
            var connectionLogicMock = new Mock<IConnectionLogic>();
            var networkMock = new Mock<INetwork>();
            var queueMock = new Mock<IConnectionMessageQueue>();
            var coalescerMock = new Mock<ISendCoalescer>();
            connectionLogicMock.SetupGet(logic => logic.Peer).Returns(playerPeer);
            var state = new LoadingState(
                connectionLogicMock.Object,
                serverComponent.TestMessageBroker,
                networkMock.Object,
                baselineSender.Object,
                kingdomBaselineSender.Object,
                queueMock.Object,
                coalescerMock.Object,
                playerManager);
            connectionLogicMock.SetupGet(logic => logic.State).Returns(state);
            using var worldReadyFlushEntered = new ManualResetEventSlim(false);
            using var releaseWorldReadyFlush = new ManualResetEventSlim(false);
            int blockWorldReadyFlush = 0;
            coalescerMock
                .Setup(value => value.Flush(networkMock.Object))
                .Callback(() =>
                {
                    if (Volatile.Read(ref blockWorldReadyFlush) == 0) return;
                    worldReadyFlushEntered.Set();
                    releaseWorldReadyFlush.Wait();
                });

            StartBaseline(state);
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            SendAndDrain(state, JoinSyncSignal.BaselineApplied);
            Volatile.Write(ref blockWorldReadyFlush, 1);
            Signal(state, JoinSyncSignal.FinalBaselineApplied);
            Assert.True(worldReadyFlushEntered.Wait(TimeSpan.FromSeconds(5)));

            Task<bool> abortTask = Task.Run(state.TryAbortJoinCatchUp);
            try
            {
                Assert.False(await abortTask.WaitAsync(TimeSpan.FromSeconds(5)));
            }
            finally
            {
                releaseWorldReadyFlush.Set();
            }

            DrainGameThread();
            Assert.True(state.TryAbortJoinCatchUp());
            Assert.True(state.IsAborted);
            queueMock.Verify(
                value => value.OpenWithTailBatch(
                    playerPeer,
                    It.IsAny<IMessage>(),
                    It.IsAny<Func<bool>>()),
                Times.Once);
            connectionLogicMock.Verify(value => value.EnterCampaign(), Times.Never);
            state.Dispose();
        }

        [Fact]
        public void DisposedConnection_DoesNotInstallReplacementState()
        {
            connectionLogic.Dispose();

            connectionLogic.SetState<CampaignState>();

            Assert.Null(connectionLogic.State);
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

        [Fact]
        public void ReplayApplied_SendsKingdomBaselineAfterReplayMarker()
        {
            var state = connectionLogic.SetState<LoadingState>();
            kingdomBaselineSender
                .Setup(sender => sender.Send(playerPeer))
                .Callback(() => serverComponent.TestNetwork.SendImmediate(
                    playerPeer,
                    new NetworkJoinCampaignKingdomBaseline(Array.Empty<PendingAllianceOfferBaseline>(), Array.Empty<PendingPeaceOfferBaseline>())));
            StartReplay(state);
            var beforeAck = serverComponent.TestNetwork.GetPeerMessages(playerPeer).ToArray();
            Assert.DoesNotContain(beforeAck, message => message is NetworkJoinCampaignKingdomBaseline);
            SendAndDrain(state, JoinSyncSignal.ReplayApplied);
            Assert.Contains(
                serverComponent.TestNetwork.GetPeerMessages(playerPeer),
                message => message is NetworkJoinCampaignKingdomBaseline);
        }

        [Fact]
        public void KingdomBaseline_SentAlongsidePartyBaselineThroughFullJoinHandshake()
        {
            var state = connectionLogic.SetState<LoadingState>();
            StartReplay(state);
            kingdomBaselineSender.Verify(sender => sender.Send(playerPeer), Times.Never);

            SendAndDrain(state, JoinSyncSignal.ReplayApplied);
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Once);
            kingdomBaselineSender.Verify(sender => sender.Send(playerPeer), Times.Once);

            Signal(state, JoinSyncSignal.BaselineApplied);
            Signal(state, JoinSyncSignal.FinalBaselineApplied);
            DrainGameThread();
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            SendAndDrain(state, JoinSyncSignal.BaselineRequested);
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(3));
            kingdomBaselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(3));

            SendAndDrain(state, JoinSyncSignal.BaselineApplied);
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(4));
            kingdomBaselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(4));

            SendAndDrain(state, JoinSyncSignal.FinalBaselineApplied);
            // both senders must have fired the same number of times at every step above —
            // if a future change moves one call out of QueueBaseline, this diverges here.
            baselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(4));
            kingdomBaselineSender.Verify(sender => sender.Send(playerPeer), Times.Exactly(4));
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

        private void CampaignEntered(LoadingState state, NetPeer? peer = null) =>
            state.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(
                    peer ?? playerPeer,
                    new NetworkPlayerCampaignEntered()));

        private void Signal(LoadingState state, JoinSyncSignal signal, int replayBatchId = 0) =>
            state.JoinSyncHandler(
                new MessagePayload<NetworkJoinSync>(
                    playerPeer,
                    new NetworkJoinSync(signal, replayBatchId)));

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
