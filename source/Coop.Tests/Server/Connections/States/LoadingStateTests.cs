using Autofac;
using Common;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using LiteNetLib;
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
        public void PlayerCampaignEntered_ValidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();

            // Act
            var payload = new MessagePayload<NetworkPlayerCampaignEntered>(
                playerPeer, new NetworkPlayerCampaignEntered());
            currentState.PlayerCampaignEnteredHandler(payload);
            DrainGameThread();

            // Assert
            Assert.Single(serverComponent.TestMessageBroker.GetMessagesFromType<PlayerCampaignEntered>());
            Assert.Single(serverComponent.TestNetwork.GetPeerMessagesFromType<NetworkJoinCatchUpComplete>(playerPeer));

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
        public void PlayerCampaignEntered_SendsCatchUpMarkerAfterJoinSnapshots()
        {
            // Arrange
            var currentState = connectionLogic.SetState<LoadingState>();
            serverComponent.TestMessageBroker.Subscribe<PlayerCampaignEntered>(_ =>
                serverComponent.TestNetwork.SendImmediate(playerPeer, new NetworkPlayerCampaignEntered()));

            // Act
            currentState.PlayerCampaignEnteredHandler(
                new MessagePayload<NetworkPlayerCampaignEntered>(playerPeer, new NetworkPlayerCampaignEntered()));
            DrainGameThread();

            // Assert
            var messages = serverComponent.TestNetwork.GetPeerMessages(playerPeer).ToArray();
            Assert.Contains(messages, message => message is NetworkPlayerCampaignEntered);
            Assert.IsType<NetworkJoinCatchUpComplete>(messages.Last());
        }

        private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
    }
}
