﻿using Coop.Core.Client;
using Coop.Core.Client.States;
using GameInterface.Services.GameState.Messages;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class CharacterCreationStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public CharacterCreationStateTests(ITestOutputHelper output) : base(output)
        {
            var mockCoopClient = new Mock<ICoopClient>();
            clientLogic = new ClientLogic(mockCoopClient.Object, messageBroker);
            clientLogic.State = new CharacterCreationState(clientLogic, messageBroker);
        }

        [Fact]
        public void Ctor_Subscribes()
        {
            var subscriberCount = messageBroker.GetTotalSubscribers();
            Assert.Equal(2, subscriberCount);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            var isEventPublished = false;
            messageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.EnterMainMenu();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void EnterMainMenu_Transitions_MainMenuState()
        {
            messageBroker.Publish(this, new MainMenuEntered());

            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Transitions_ReceivingSavedDataState()
        {
            messageBroker.Publish(this, new CharacterCreationFinished());

            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Publishes_LoadGameSave()
        {
            var isEventPublished = false;
            messageBroker.Subscribe<LoadGameSave>((payload) =>
            {
                isEventPublished = true;
            });

            messageBroker.Publish(this, new CharacterCreationFinished());

            Assert.True(isEventPublished);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            var isEventPublished = false;
            messageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.Disconnect();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            clientLogic.Dispose();

            var subscriberCount = messageBroker.GetTotalSubscribers();
            Assert.Equal(0, subscriberCount);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            clientLogic.Connect();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<CharacterCreationState>(clientLogic.State);
        }
    }
}
