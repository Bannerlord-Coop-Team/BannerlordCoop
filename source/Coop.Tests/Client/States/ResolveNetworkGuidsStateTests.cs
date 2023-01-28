using Coop.Core.Client;
using Coop.Core.Client.States;
using GameInterface.Services.GameState.Messages;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class ResolveNetworkGuidsStateTests : CoopTest
    {
        private readonly IClientLogic clientLogic;
        public ResolveNetworkGuidsStateTests(ITestOutputHelper output) : base(output)
        {
            var mockCoopClient = new Mock<ICoopClient>();
            clientLogic = new ClientLogic(mockCoopClient.Object, MessageBroker);
            clientLogic.State = new ResolveNetworkGuidsState(clientLogic, MessageBroker);
        }

        [Fact]
        public void Ctor_Subscribes()
        {
            var subscriberCount = MessageBroker.GetTotalSubscribers();
            Assert.Equal(2, subscriberCount);
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
        public void EnterMainMenu_Transitions_MainMenuState()
        {
            MessageBroker.Publish(this, new MainMenuEntered());

            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void EnterMainMenu_Transitions_CampaignState()
        {
            MessageBroker.Publish(this, new CampaignStateEntered());

            Assert.IsType<CampaignState>(clientLogic.State);
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
        public void Dispose_RemovesAllHandlers()
        {
            clientLogic.Dispose();

            var subscriberCount = MessageBroker.GetTotalSubscribers();
            Assert.Equal(0, subscriberCount);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            clientLogic.Connect();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);
        }
    }
}
