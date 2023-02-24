using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
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
            clientLogic = new ClientLogic(mockCoopClient.Object, NetworkMessageBroker);
            clientLogic.State = new ResolveNetworkGuidsState(clientLogic);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            Assert.NotEqual(0, MessageBroker.GetTotalSubscribers());

            clientLogic.State.Dispose();

            Assert.Equal(0, MessageBroker.GetTotalSubscribers());
        }

        [Fact]
        public void ValidateModuleState_EntryEvents()
        {
            // Setup event callbacks
            var resolveNetworkGuidsCount = 0;
            MessageBroker.Subscribe<ResolveNetworkGuids>((payload) =>
            {
                resolveNetworkGuidsCount += 1;
            });

            // Trigger state entry
            clientLogic.State = new ResolveNetworkGuidsState(clientLogic);

            // All events are called exactly once
            Assert.Equal(1, resolveNetworkGuidsCount);
        }

        [Fact]
        public void NetworkGuidsResolved_Transitions_CampaignState()
        {
            MessageBroker.Publish(this, new NetworkGuidsResolved());

            Assert.IsType<CampaignState>(clientLogic.State);
        }

        [Fact]
        public void EnterCampaignState_Transitions_CampaignState()
        {
            clientLogic.EnterCampaignState();

            Assert.IsType<CampaignState>(clientLogic.State);
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
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.LoadSavedData();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.ResolveNetworkGuids();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<ResolveNetworkGuidsState>(clientLogic.State);
        }
    }
}
