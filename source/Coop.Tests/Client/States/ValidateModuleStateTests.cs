using Autofac;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI.Interfaces;
using LiteNetLib;
using Moq;
using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States
{
    public class ValidateModuleStateTests : IDisposable
    {
        private readonly IClientLogic clientLogic;
        private readonly NetPeer serverPeer;
        private readonly ClientTestComponent clientComponent;

        public ValidateModuleStateTests(ITestOutputHelper output)
        {
            clientComponent = new ClientTestComponent(output);
            var container = clientComponent.Container;

            serverPeer = clientComponent.TestNetwork.CreatePeer();
            clientLogic = container.Resolve<IClientLogic>()!;
        }

        public void Dispose()
        {
            // Every test enters ValidateModuleState, which arms a 30s validation-timeout Timer. Dispose
            // the logic (and thus the current state) so that timer is torn down with the test instead of
            // lingering and firing TimeoutValidation on a stale state after the test has finished.
            clientLogic.Dispose();
        }

        [Fact]
        public void ValidateModuleState_EntryEvents()
        {
            // Act
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Assert
            Assert.NotEmpty(clientComponent.TestNetwork.Peers);

            var message = Assert.Single(clientComponent.TestNetwork.GetPeerMessages(serverPeer));
            Assert.IsType<NetworkModuleVersionsValidate>(message);
        }

        [Fact]
        public void NetworkModuleVersionsValidated_Transitions_ReceiveResult()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            var payload = new MessagePayload<NetworkModuleVersionsValidated>(
                this, new NetworkModuleVersionsValidated(true, null));

            // Act
            validateState.Handle_NetworkModuleVersionsValidated(payload);

            // Assert
            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }

        [Fact]
        public void NetworkClientValidated_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            var heroExists = true;
            var payload = new MessagePayload<NetworkClientValidated>(
                this, new NetworkClientValidated(heroExists, new Player("12345", "111", "12345", "12345", "12345")));

            // Act
            validateState.Handle_NetworkClientValidated(payload);

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void NetworkClientValidated_Publishes_StartCharacterCreation()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            var heroExists = false;
            var payload = new MessagePayload<NetworkClientValidated>(
                this, new NetworkClientValidated(heroExists, new Player("12345", "111", "12345", "12345", "12345")));

            // Act
            validateState.Handle_NetworkClientValidated(payload);

            // Assert
            var message = Assert.Single(clientComponent.TestMessageBroker.Messages);
            Assert.IsType<StartCharacterCreation>(message);
        }

        [Fact]
        public void NetworkModuleVersionsValidated_Denied_HidesLoadingScreenAndShowsReason()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            var payload = new MessagePayload<NetworkModuleVersionsValidated>(
                this, new NetworkModuleVersionsValidated(false, "Wrong version of module 'Coop'"));

            // Act
            validateState.Handle_NetworkModuleVersionsValidated(payload);

            // Assert — the denial must tear coop down AND release the forced loading window; the
            // reason must reach the pop-up (the information message lands in the chat log, which is
            // hidden behind the loading screen the player is looking at).
            Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());

            clientComponent.Container
                .Resolve<Mock<ILoadingInterface>>()
                .Verify(li => li.HideLoadingScreen(), Times.Once);

            var popup = Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<SendPopupMessage>());
            Assert.Contains("Wrong version of module 'Coop'", popup.Text);
        }

        [Fact]
        public void ValidationTimeout_DisconnectsWithReason()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act — invoke the deadline logic directly (the Timer -> GameThread marshaling is glue).
            validateState.TimeoutValidation();

            // Assert — a server that never answers (validation crashed server-side, incompatible
            // build) must not leave the player on the loading screen forever.
            Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());

            clientComponent.Container
                .Resolve<Mock<ILoadingInterface>>()
                .Verify(li => li.HideLoadingScreen(), Times.Once);

            var popup = Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<SendPopupMessage>());
            Assert.Contains("Timed out", popup.Text);
        }

        [Fact]
        public void ValidationTimeout_AfterStateLeft_DoesNothing()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();
            clientLogic.LoadSavedData(); // transitions away, disposing the state

            clientComponent.TestMessageBroker.Messages.Clear();

            // Act — a timer callback that was already in flight when the state was left must no-op.
            validateState.TimeoutValidation();

            // Assert
            Assert.Empty(clientComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());
        }

        [Fact]
        public void Disconnect_CalledTwice_FinalizesCoopOnce()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act — teardown can be raced by the timeout timer (game thread) and a denied/late
            // response (poller thread); a second entry must be idempotent.
            validateState.Disconnect();
            validateState.Disconnect();

            // Assert — the latch means CoopFinalizer runs exactly once, not once per caller.
            Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());
            clientComponent.Container
                .Resolve<Mock<ILoadingInterface>>()
                .Verify(li => li.HideLoadingScreen(), Times.Once);
        }

        [Fact]
        public void ValidationTimeout_AfterDenial_DoesNotFinalizeAgain()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();
            var denial = new MessagePayload<NetworkModuleVersionsValidated>(
                this, new NetworkModuleVersionsValidated(false, "Wrong version of module 'Coop'"));
            validateState.Handle_NetworkModuleVersionsValidated(denial); // tears coop down

            // Act — a timeout callback that fires just after the denial already tore coop down must no-op.
            validateState.TimeoutValidation();

            // Assert — still a single teardown; the timeout did not tear down again.
            Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());
        }

        [Fact]
        public void EnterMainMenu_DoesNothing()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act
            clientLogic.EnterMainMenu();

            // Assert — EnterMainMenu is a no-op in this state; teardown happens via Disconnect.
            Assert.IsType<ValidateModuleState>(clientLogic.State);
        }

        [Fact]
        public void LoadSavedData_Transitions_ReceivingSavedDataState()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act
            clientLogic.LoadSavedData();

            // Assert
            Assert.IsType<ReceivingSavedDataState>(clientLogic.State);
        }

        [Fact]
        public void Disconnect_FinalizesCoop()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act
            clientLogic.Disconnect();

            // Assert — validation-failure disconnect tears coop down (EndCoopMode) even pre-campaign,
            // rather than relying on GoToMainMenu -> MainMenuEntered (which no-ops with no campaign).
            Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());
        }

        [Fact]
        public void StartCharacterCreation_Publishes_StartCharacterCreation()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act
            clientLogic.StartCharacterCreation();

            // Assert
            var message = Assert.Single(clientComponent.TestMessageBroker.Messages);
            Assert.IsType<StartCharacterCreation>(message);
        }

        [Fact]
        public void CharacterCreationStarted_Transitions_CharacterCreationState()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            var payload = new MessagePayload<CharacterCreationStarted>(
                this, new CharacterCreationStarted());

            // Act
            validateState.Handle_CharacterCreationStarted(payload);

            // Assert
            Assert.IsType<CharacterCreationState>(clientLogic.State);
        }

        [Fact]
        public void OtherStateMethods_DoNotAlterState()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            // Act
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
