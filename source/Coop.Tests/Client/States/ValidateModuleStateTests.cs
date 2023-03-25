using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Modules.Messages;
using GameInterface.Services.Time.Messages;
using LiteNetLib.Utils;
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
            clientLogic = new ClientLogic(mockCoopClient.Object, StubNetworkMessageBroker);
            clientLogic.State = new ValidateModuleState(clientLogic);
        }

        [Fact]
        public void Dispose_RemovesAllHandlers()
        {
            Assert.NotEqual(0, StubMessageBroker.GetTotalSubscribers());

            clientLogic.State.Dispose();

            Assert.Equal(0, StubMessageBroker.GetTotalSubscribers());
        }

        [Fact]
        public void ValidateModuleState_EntryEvents()
        {
            // Setup event callbacks
            var networkClientValidateCount = 0;
            StubNetworkMessageBroker.TestNetworkSubscribe<NetworkClientValidate>((payload) =>
            {
                networkClientValidateCount += 1;
            });

            // Trigger state entry
            clientLogic.State = new ValidateModuleState(clientLogic);

            // All events are called exactly once
            Assert.Equal(1, networkClientValidateCount);
        }

        [Fact]
        public void NetworkClientValidated_Tranitions_ReceivingSavedDataState()
        {
            StubNetworkMessageBroker.ReceiveNetworkEvent(null, new NetworkClientValidated(true, string.Empty));

            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void NetworkClientValidated_Tranitions_CharacterCreationState()
        {
            var startCharacterCreationCount = 0;
            StubMessageBroker.Subscribe<StartCharacterCreation>((payload) =>
            {
                startCharacterCreationCount += 1;
            });

            StubNetworkMessageBroker.ReceiveNetworkEvent(null, new NetworkClientValidated(false, string.Empty));

            Assert.Equal(1, startCharacterCreationCount);
        }

        [Fact]
        public void EnterMainMenu_Publishes_EnterMainMenuEvent()
        {
            var enterMainMenuCount = 0;
            StubMessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                enterMainMenuCount += 1;
            });

            clientLogic.EnterMainMenu();

            // All events are called exactly once
            Assert.Equal(1, enterMainMenuCount);
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
        public void Disconnect_Publishes_EnterMainMenu()
        {
            var isEventPublished = false;
            StubMessageBroker.Subscribe<EnterMainMenu>((payload) =>
            {
                isEventPublished = true;
            });

            clientLogic.Disconnect();

            Assert.True(isEventPublished);
        }

        [Fact]
        public void StartCharacterCreation_Publishes_StartCharacterCreation()
        {
            var startCharacterCreationCount = 0;
            StubMessageBroker.Subscribe<StartCharacterCreation>((payload) =>
            {
                startCharacterCreationCount += 1;
            });

            clientLogic.StartCharacterCreation();

            // All events are called exactly once
            Assert.Equal(1, startCharacterCreationCount);
        }

        [Fact]
        public void CharacterCreationStarted_Transitions_CharacterCreationState()
        {
            StubMessageBroker.Publish(this, new CharacterCreationStarted());

            Assert.IsType<CharacterCreationState>(clientLogic.State);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            clientLogic.Connect();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.Disconnect();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.EnterCampaignState();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.EnterMissionState();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.ExitGame();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.StartCharacterCreation();
            Assert.IsType<ValidateModuleState>(clientLogic.State);

            clientLogic.ValidateModules();
            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }
    }
}
