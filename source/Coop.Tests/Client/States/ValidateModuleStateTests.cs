using Coop.Core.Client;
using Coop.Core.Client.States;
using GameInterface.Services.GameState.Messages;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class ValidateModuleStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public ValidateModuleStateTests(ITestOutputHelper output) : base(output)
        {
            var mockCoopClient = new Mock<ICoopClient>();
            clientLogic = new ClientLogic(mockCoopClient.Object, messageBroker);
            clientLogic.State = new ValidateModuleState(clientLogic, messageBroker);
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
        public void LoadSavedData_Publishes_ValidatedModule()
        {
            var isEventPublished = false;
            messageBroker.Subscribe<ValidateModule>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.LoadSavedData();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void ModulesValidated_Transitions_LoadingState()
        {
            messageBroker.Publish(this, new ModulesValidated());

            Assert.IsType<LoadingState>(clientLogic.State);
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
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }
    }
}
