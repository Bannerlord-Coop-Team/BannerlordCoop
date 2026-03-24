using Autofac;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Modules;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

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

            var modules = new List<ModuleInfo> { new ModuleInfo("1", true, new ApplicationVersion()) };

            serverComponent.ModuleInfoProviderMock
                .Setup(mip => mip.GetModuleInfos())
                .Returns(modules);

            // Act
            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules));
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

            serverComponent.ModuleInfoProviderMock
                .Setup(mip => mip.GetModuleInfos())
                .Returns(
                    new List<ModuleInfo> { new ModuleInfo("1", true, new ApplicationVersion()) }
                );

            // Act
            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(new List<ModuleInfo> { new ModuleInfo("MismatchedModule", true, new ApplicationVersion())}));
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
            string heroId = "MyHero";

            serverComponent.HeroInterfaceMock
                .Setup(i => i.TryResolveHero(playerId, out It.Ref<string>.IsAny))
                .Callback((string id, out string returnedHeroId) =>
                {
                    returnedHeroId = heroId;
                })
                .Returns(true);

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                playerPeer, new NetworkClientValidate(playerId));
            currentState.ClientValidateHandler(payload);

            // Assert
            var messages = serverComponent.TestNetwork.SentNetworkMessages[playerPeer.Id];

            var validated = messages.OfType<NetworkClientValidated>();

            var message = Assert.Single(validated);

            Assert.True(message.HeroExists);
            Assert.Equal(heroId, message.HeroId);
        }

        [Fact]
        public void NetworkClientValidate_InvalidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            string playerId = "MyPlayer";

            serverComponent.HeroInterfaceMock
                .Setup(i => i.TryResolveHero(playerId, out It.Ref<string>.IsAny))
                .Callback((string id, out string returnedHeroId) =>
                {
                    returnedHeroId = string.Empty;
                })
                .Returns(false);

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                differentPeer, new NetworkClientValidate(playerId));
            currentState.ClientValidateHandler(payload);

            // Assert
            var messages = serverComponent.TestNetwork.SentNetworkMessages
                .GetValueOrDefault(playerPeer.Id) ?? Enumerable.Empty<IMessage>();

            Assert.Empty(messages.OfType<NetworkClientValidated>());
        }
    }
}
