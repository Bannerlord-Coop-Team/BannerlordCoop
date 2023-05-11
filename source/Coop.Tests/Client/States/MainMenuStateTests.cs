using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.States;
using GameInterface.Services.GameState.Messages;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class MainMenuStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public MainMenuStateTests(ITestOutputHelper output) : base(output)
        {
            var mockCoopClient = new Mock<ICoopClient>();
            clientLogic = new ClientLogic(mockCoopClient.Object, StubNetworkMessageBroker);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            Assert.NotEqual(0, StubMessageBroker.GetTotalSubscribers());

            clientLogic.State.Dispose();

            Assert.Equal(0, StubMessageBroker.GetTotalSubscribers());
        }

        [Fact]
        public void ValidateModulesMethod_Transitions_ValidateModuleState()
        {
            clientLogic.State.ValidateModules();

            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }

        [Fact]
        public void Connect_ValidateModuleState()
        {
            StubMessageBroker.Publish(this, new NetworkConnected());

            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            var enterMainMenuCount = 0;
            StubMessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                enterMainMenuCount += 1;
            });

            clientLogic.Disconnect();

            Assert.Equal(1, enterMainMenuCount);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            clientLogic.Disconnect();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.EnterMainMenu();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<MainMenuState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<MainMenuState>(clientLogic.State);
        }
    }
}
