using Autofac;
using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Modules;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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
        public void TransferSaveMethod_TransitionState_LoadingState()
        {
            // Arrange
            connectionLogic.SetState<ResolveCharacterState>();

            // Act
            connectionLogic.TransferSave();

            // Assert — TransferSave sends the save (TransferSaveState) then immediately advances to
            // LoadingState to await the client entering the campaign.
            Assert.IsType<LoadingState>(connectionLogic.State);
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

            serverComponent.Container
                .Resolve<Mock<IModuleInfoProvider>>()
                .Setup(mip => mip.GetModuleInfos())
                .Returns(modules);

            // Act
            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules));
            currentState.Handle_ModuleVersionsValidate(payload);

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

            serverComponent.Container
                .Resolve<Mock<IModuleInfoProvider>>()
                .Setup(mip => mip.GetModuleInfos())
                .Returns(
                    new List<ModuleInfo> { new ModuleInfo("1", true, new ApplicationVersion()) }
                );

            // Act
            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(new List<ModuleInfo> { new ModuleInfo("MismatchedModule", true, new ApplicationVersion())}));
            currentState.Handle_ModuleVersionsValidate(payload);

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

            

            var player = new Player("MyPlayer", "MyHero", "MyParty", "MyClan", "MyCharacter");

            var playerManagerMock = serverComponent.Container.Resolve<Mock<IPlayerManager>>();

            playerManagerMock
                .Setup(i => i.TryGetPlayer(player.ControllerId, out It.Ref<Player>.IsAny))
                .Callback((string id, out Player returnedPlayer) =>
                {
                    returnedPlayer = player;
                })
                .Returns(true);

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                playerPeer, new NetworkClientValidate(player.ControllerId));
            currentState.Handle_ClientValidate(payload);

            // Assert
            var messages = serverComponent.TestNetwork.SentNetworkMessages[playerPeer.Id];

            var validated = messages.OfType<NetworkClientValidated>();

            var message = Assert.Single(validated);

            Assert.True(message.HeroExists);
            Assert.Equal(player, message.Player);
        }

        [Fact]
        public void NetworkClientValidate_InvalidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            string playerId = "MyPlayer";

            serverComponent.Container
                .Resolve<Mock<IPlayerManager>>()
                .Setup(i => i.TryGetPlayer(playerId, out It.Ref<Player>.IsAny))
                .Callback((string id, out Player? returnedPlayer) =>
                {
                    returnedPlayer = null;
                })
                .Returns(false);

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                differentPeer, new NetworkClientValidate(playerId));
            currentState.Handle_ClientValidate(payload);

            // Assert
            var messages = serverComponent.TestNetwork.SentNetworkMessages
                .GetValueOrDefault(playerPeer.Id) ?? Enumerable.Empty<IMessage>();

            Assert.Empty(messages.OfType<NetworkClientValidated>());
        }
    }
}
