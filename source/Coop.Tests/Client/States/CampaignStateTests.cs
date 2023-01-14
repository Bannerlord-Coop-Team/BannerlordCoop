﻿using Coop.Core.Client;
using Coop.Core.Client.States;
using GameInterface.Services.GameState.Messages;
using Moq;
using Serilog;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class CampaignStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public CampaignStateTests(ITestOutputHelper output) : base(output)
        {
            var mockCoopClient = new Mock<ICoopClient>();
            clientLogic = new ClientLogic(mockCoopClient.Object, messageBroker);
            clientLogic.State = new CampaignState(clientLogic, messageBroker);
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
        public void MissionState_Transitions_MissionState()
        {
            messageBroker.Publish(this, new MissionStateEntered());

            Assert.IsType<MissionState>(clientLogic.State);
        }

        [Fact]
        public void EnterMissionState_Publishes_EnterMissionState()
        {
            var isEventPublished = false;
            messageBroker.Subscribe<EnterMissionState>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.EnterMissionState();

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
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<CampaignState>(clientLogic.State);
        }
    }
}
