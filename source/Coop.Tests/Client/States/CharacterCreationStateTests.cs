using Autofac;
using Common;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.AdvancedStart.Data;
using GameInterface.Services.AdvancedStart.Interfaces;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class CharacterCreationStateTests
    {
        private readonly IClientLogic clientLogic;
        private readonly NetPeer serverPeer;
        private readonly ClientTestComponent clientComponent;

        private TestMessageBroker TestMessageBroker => clientComponent.TestMessageBroker;
        private TestNetwork MockNetwork => clientComponent.TestNetwork;

        public CharacterCreationStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            serverPeer = MockNetwork.CreatePeer();
            clientLogic = container.Resolve<IClientLogic>()!;
        }

        [Fact]
        public void NewPlayerHeroRegistered_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            var player = new Player("MyControllerId", "MyHeroId", "MyPartyId", "MyClanId", "MyCharacterObjectId");

            var payload = new MessagePayload<NetworkHeroRecieved>(
                this, new NetworkHeroRecieved(player));

            // Act
            characterCreationState.Handle_NetworkHeroRecieved(payload);

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PolicyAndCreationFinished_InEitherOrder_SendHeroOnceWithDefaultStart(bool policyFirst)
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();
            byte[] heroData = { 1, 2, 3 };
            clientComponent.Container.Resolve<Mock<IHeroInterface>>()
                .Setup(x => x.PackageMainHero())
                .Returns(heroData);

            // Act
            if (policyFirst) ReceivePolicy(characterCreationState);
            FinishCharacterCreation(characterCreationState);
            if (!policyFirst)
            {
                Assert.Empty(MockNetwork.GetPeerMessagesFromType<NetworkTransferNewHero>(serverPeer));
                ReceivePolicy(characterCreationState);
            }
            // The server pushes a new policy whenever it changes, which mustn't send the hero again.
            ReceivePolicy(characterCreationState);

            // Assert
            Assert.Single(MockNetwork.GetPeerMessagesFromType<NetworkRequestAdvancedStartPolicy>(serverPeer));
            var message = Assert.Single(MockNetwork.GetPeerMessagesFromType<NetworkTransferNewHero>(serverPeer));
            Assert.Equal(heroData, message.PlayerHero);
            Assert.Empty(message.AdvancedStart);
        }

        [Fact]
        public void ShownWindow_HoldsHeroUntilJoin_ThenSendsPicks()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();
            Action<AdvancedStartSelection[]> onJoin = null;
            clientComponent.Container.Resolve<Mock<IAdvancedStartInterface>>()
                .Setup(x => x.TryShowStartOptions(It.IsAny<Action<AdvancedStartSelection[]>>(), It.IsAny<Action>()))
                .Callback<Action<AdvancedStartSelection[]>, Action>((join, _) => onJoin = join)
                .Returns(true);

            // Act
            ReceivePolicy(characterCreationState);
            FinishCharacterCreation(characterCreationState);

            // Assert
            Assert.Empty(MockNetwork.GetPeerMessagesFromType<NetworkTransferNewHero>(serverPeer));

            // Act
            onJoin(new[] { new AdvancedStartSelection("StartType", "outlaw") });

            // Assert
            var message = Assert.Single(MockNetwork.GetPeerMessagesFromType<NetworkTransferNewHero>(serverPeer));
            var selection = Assert.Single(message.AdvancedStart);
            Assert.Equal("StartType", selection.Id);
            Assert.Equal("outlaw", selection.Value);
        }

        [Fact]
        public void WindowThatThrows_JoinsWithDefaultStart()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();
            clientComponent.Container.Resolve<Mock<IAdvancedStartInterface>>()
                .Setup(x => x.TryShowStartOptions(It.IsAny<Action<AdvancedStartSelection[]>>(), It.IsAny<Action>()))
                .Throws(new InvalidOperationException("window failed"));

            // Act
            ReceivePolicy(characterCreationState);
            FinishCharacterCreation(characterCreationState);

            // Assert
            var message = Assert.Single(MockNetwork.GetPeerMessagesFromType<NetworkTransferNewHero>(serverPeer));
            Assert.Empty(message.AdvancedStart);
        }

        private void ReceivePolicy(CharacterCreationState characterCreationState)
        {
            characterCreationState.Handle_NetworkAdvancedStartPolicy(new MessagePayload<NetworkAdvancedStartPolicy>(
                serverPeer, new NetworkAdvancedStartPolicy(null)));
            GameThread.Run(() => { }, blocking: true);
        }

        private void FinishCharacterCreation(CharacterCreationState characterCreationState)
        {
            characterCreationState.Handle_CharacterCreationFinished(new MessagePayload<CharacterCreationFinished>(
                this, new CharacterCreationFinished()));
        }

        [Fact]
        public void EnterMainMenu_GoesToMainMenu()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();
            var gameStateMock = clientComponent.Container.Resolve<Mock<IGameStateInterface>>();

            // Act
            clientLogic.EnterMainMenu();

            // Assert
            gameStateMock.Verify(x => x.GoToMainMenu(), Times.Once);
        }

        [Fact]
        public void MainMenuEntered_Transitions_MainMenuState()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            var payload = new MessagePayload<MainMenuEntered>(
                this, new MainMenuEntered());

            // Act
            characterCreationState.Handle_MainMenuEntered(payload);

            // Assert
            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void LoadSavedData_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            // Act
            clientLogic.LoadSavedData();

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            var characterCreationState = clientLogic.SetState<CharacterCreationState>();

            // Act
            clientLogic.Connect();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<CharacterCreationState>(clientLogic.State);
        }
    }
}
