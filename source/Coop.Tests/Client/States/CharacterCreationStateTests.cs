using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
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
            clientLogic = new ClientLogic(mockCoopClient.Object, StubNetworkMessageBroker);
            clientLogic.State = new CharacterCreationState(clientLogic);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            Assert.NotEqual(0, StubMessageBroker.GetTotalSubscribers());

            clientLogic.State.Dispose();

            Assert.Equal(0, StubMessageBroker.GetTotalSubscribers());
        }

        [Fact]
        public void HeroPackaged_Publishes_NetworkTransferedHero()
        {
            var networkTransferedHeroCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkTransferedHero>((payload) =>
            {
                networkTransferedHeroCount += 1;
            });

            StubNetworkMessageBroker.Publish(this, new NewHeroPackaged());

            Assert.Equal(1, networkTransferedHeroCount);
        }

        [Fact]
        public void HeroPackaged_Transitions_ReceivingSavedDataState()
        {
            StubNetworkMessageBroker.Publish(this, new NewHeroPackaged());

            clientLogic.EnterMainMenu();

            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void CharacterCreationFinished_Publishes_PackageMainHero()
        {
            var packageMainHeroCount = 0;
            StubNetworkMessageBroker.Subscribe<PackageMainHero>((payload) =>
            {
                packageMainHeroCount += 1;
            });

            StubNetworkMessageBroker.Publish(this, new CharacterCreationFinished());

            Assert.Equal(1, packageMainHeroCount);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            var isEventPublished = false;
            StubMessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.EnterMainMenu();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void EnterMainMenu_Transitions_MainMenuState()
        {
            StubMessageBroker.Publish(this, new MainMenuEntered());

            Assert.IsType<MainMenuState>(clientLogic.State);
        }

        [Fact]
        public void LoadSavedData_Transitions_ReceivingSavedDataState()
        {
            clientLogic.LoadSavedData();

            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
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

            clientLogic.StartCharacterCreation();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.ResolveNetworkGuids();
            Assert.IsType<CharacterCreationState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<CharacterCreationState>(clientLogic.State);
        }
    }
}
