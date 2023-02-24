using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Time.Messages;
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
            clientLogic = new ClientLogic(mockCoopClient.Object, NetworkMessageBroker);
            clientLogic.State = new CampaignState(clientLogic);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            Assert.NotEqual(0, MessageBroker.GetTotalSubscribers());

            clientLogic.State.Dispose();

            Assert.Equal(0, MessageBroker.GetTotalSubscribers());
        }

        [Fact]
        public void NetworkDisableTimeControls_Publishes_PauseAndDisableGameTimeControls()
        {
            var isEventPublished = false;
            MessageBroker.Subscribe<PauseAndDisableGameTimeControls>((payload) =>
            {
                isEventPublished = true;
            });

            NetworkMessageBroker.ReceiveNetworkEvent(null, new NetworkDisableTimeControls());

            Assert.True(isEventPublished);
        }

        [Fact]
        public void EnterMissionState_Publishes_EnterMissionState()
        {
            var isEventPublished = false;
            MessageBroker.Subscribe<EnterMissionState>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.EnterMissionState();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void MissionState_Transitions_MissionState()
        {
            MessageBroker.Publish(this, new MissionStateEntered());

            Assert.IsType<MissionState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            var isEventPublished = false;
            MessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.EnterMainMenu();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void MainMenuEntered_Transitions_MainMenuState()
        {
            MessageBroker.Publish(this, new MainMenuEntered());

            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_Publishes_EnterMainMenu()
        {
            var isEventPublished = false;
            MessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.Disconnect();

            Assert.True(isEventPublished);
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

            clientLogic.ResolveNetworkGuids();
            Assert.IsType<CampaignState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<CampaignState>(clientLogic.State);
        }
    }
}
