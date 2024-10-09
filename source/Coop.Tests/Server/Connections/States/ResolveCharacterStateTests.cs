using System.Collections.Generic;
using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System.Linq;
using GameInterface.Services.Modules;
using Xunit;
using Xunit.Abstractions;
using Coop.Core.Client.States;
using TaleWorlds.Library;

namespace Coop.Tests.Server.Connections.States
{
    public class ResolveCharacterStateTests
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        public ResolveCharacterStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new NamedParameter("playerId", playerPeer));
        }

        [Fact]
        public void CreateCharacterMethod_TransitionState_CreateCharacterState()
        {
            // Arrange
            connectionLogic.SetState<ResolveCharacterState>();

            // Act
            connectionLogic.CreateCharacter();

            // Assert
            Assert.IsType<CreateCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void TransferSaveMethod_TransitionState_TransferSaveState()
        {
            // Arrange
            connectionLogic.SetState<ResolveCharacterState>();

            // Act
            connectionLogic.TransferSave();

            // Assert
            Assert.IsType<TransferSaveState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.SetState<ResolveCharacterState>();

            // Act
            connectionLogic.Load();
            connectionLogic.EnterCampaign();
            connectionLogic.EnterMission();

            // Assert
            Assert.IsType<ResolveCharacterState>(connectionLogic.State);
        }
        
        [Fact]
        public void NetworkModuleVersionsValidate_ModulesMatches()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            // Act
            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(new List<ModuleInfo>()));
            currentState.ModuleVersionsValidateHandler(payload);

            // Assert
            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer));
            Assert.IsType<NetworkModuleVersionsValidated>(message);

            var castedMessage = (NetworkModuleVersionsValidated)message;
            Assert.True(castedMessage.Matches);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_ModulesMismatch()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            // Act
            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(new List<ModuleInfo> { new ModuleInfo("1", true, new ApplicationVersion())}));
            currentState.ModuleVersionsValidateHandler(payload);

            // Assert
            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer));
            Assert.IsType<NetworkModuleVersionsValidated>(message);

            var castedMessage = (NetworkModuleVersionsValidated)message;
            Assert.False(castedMessage.Matches);
        }

        [Fact]
        public void NetworkClientValidate_ValidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            string playerId = "MyPlayer";

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                playerPeer, new NetworkClientValidate(playerId));
            currentState.ClientValidateHandler(payload);

            // Assert
            var message = Assert.Single(serverComponent.TestMessageBroker.Messages);
            Assert.IsType<ResolveHero>(message);

            var castedMessage = (ResolveHero)message;
            Assert.Equal(playerId, castedMessage.PlayerId);
        }

        [Fact]
        public void NetworkClientValidate_InvalidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            string playerId = "MyPlayer";

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                differentPeer, new NetworkClientValidate(playerId));
            currentState.ClientValidateHandler(payload);

            // Assert
            Assert.Empty(serverComponent.TestMessageBroker.Messages);
        }

        [Fact]
        public void ResolveHero_Valid()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            string playerId = "MyPlayer";

            // Act
            var payload = new MessagePayload<HeroResolved>(
                playerPeer, new HeroResolved(playerId));
            currentState.ResolveHeroHandler(payload);

            // Assert
            Assert.Equal(2, serverComponent.TestNetwork.GetPeerMessages(playerPeer).Count());
            var message = serverComponent.TestNetwork.GetPeerMessages(playerPeer).First();
            Assert.IsType<NetworkClientValidated>(message);

            var castedMessage = (NetworkClientValidated)message;
            Assert.Equal(playerId, castedMessage.HeroId);
            Assert.True(castedMessage.HeroExists);

            Assert.Single(serverComponent.TestNetwork.GetPeerMessages(differentPeer));
        }

        [Fact]
        public void HeroNotFound_Valid()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            // Act
            var payload = new MessagePayload<ResolveHeroNotFound>(
                playerPeer, new ResolveHeroNotFound());
            currentState.HeroNotFoundHandler(payload);

            // Assert
            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer));
            Assert.IsType<NetworkClientValidated>(message);

            var castedMessage = (NetworkClientValidated)message;
            Assert.Equal(string.Empty, castedMessage.HeroId);
            Assert.False(castedMessage.HeroExists);

            Assert.False(serverComponent.TestNetwork.SentNetworkMessages.ContainsKey(differentPeer.Id));
        }
    }
}
