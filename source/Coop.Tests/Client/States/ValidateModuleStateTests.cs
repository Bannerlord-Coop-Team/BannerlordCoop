using Autofac;
using Common;
using Common.Messaging;
using Common.Network.Session;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Common;
using Coop.Core.Common.Configuration;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.Modules;
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
        public void NetworkModuleVersionsValidated_UnsupportedCoop_ContinuesValidation()
        {
            // Arrange
            var validateState = clientLogic.SetState<ValidateModuleState>();

            var payload = new MessagePayload<NetworkModuleVersionsValidated>(
                this, new NetworkModuleVersionsValidated(false, "Server does not support module 'Coop'."));

            // Act
            validateState.Handle_NetworkModuleVersionsValidated(payload);

            // Assert
            Assert.Single(clientComponent.TestNetwork.GetPeerMessages(serverPeer).OfType<NetworkClientValidate>());
            Assert.Empty(clientComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());
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
        public void ControllerIdentityPersistenceFailure_FinalizesWithVisibleReason()
        {
            var logic = new Mock<IClientLogic>();
            var controllerIdProvider = new Mock<IControllerIdProvider>();
            controllerIdProvider
                .Setup(provider => provider.SetControllerAsLocalId())
                .Throws(new InvalidOperationException("identity file is not writable"));
            var coopFinalizer = new Mock<ICoopFinalizer>();
            ValidateModuleState validateState = null!;
            logic.SetupGet(client => client.State).Returns(() => validateState);

            validateState = new ValidateModuleState(
                logic.Object,
                clientComponent.TestMessageBroker,
                clientComponent.TestNetwork,
                controllerIdProvider.Object,
                coopFinalizer.Object,
                new Mock<IGameStateInterface>().Object,
                new Mock<IModuleInfoProvider>().Object);

            try
            {
                DrainGameThread();

                coopFinalizer.Verify(
                    finalizer => finalizer.Finalize(It.Is<string>(reason =>
                        reason.Contains("persistent player identity"))),
                    Times.Once);
                Assert.Empty(clientComponent.TestNetwork.SentNetworkMessages);
            }
            finally
            {
                validateState.Dispose();
            }
        }

        [Theory]
        [InlineData("steam", "76561198000000001")]
        [InlineData("gog", "123456789")]
        public void TunneledConnection_UsesAuthenticatedTransportIdentity(
            string providerName,
            string platformUserId)
        {
            var logic = new Mock<IClientLogic>();
            var controllerIdProvider = new Mock<IControllerIdProvider>();
            var coopFinalizer = new Mock<ICoopFinalizer>();
            var moduleInfoProvider = new Mock<IModuleInfoProvider>();
            moduleInfoProvider.Setup(provider => provider.GetModuleInfos()).Returns(Array.Empty<ModuleInfo>());
            var transportTargetSource = new Mock<ISessionTransportTargetSource>();
            var identity = new PlatformIdentity(providerName, platformUserId);
            transportTargetSource.SetupGet(source => source.TunnelTarget).Returns(identity);
            ValidateModuleState validateState = null!;
            logic.SetupGet(client => client.State).Returns(() => validateState);

            validateState = new ValidateModuleState(
                logic.Object,
                clientComponent.TestMessageBroker,
                clientComponent.TestNetwork,
                controllerIdProvider.Object,
                coopFinalizer.Object,
                new Mock<IGameStateInterface>().Object,
                moduleInfoProvider.Object,
                new NetworkConfig { IsTunneled = true },
                transportTargetSource.Object);

            try
            {
                controllerIdProvider.Verify(
                    provider => provider.SetControllerAsPlatformIdentity(identity),
                    Times.Once);
                controllerIdProvider.Verify(provider => provider.SetControllerAsLocalId(), Times.Never);
                controllerIdProvider.Verify(provider => provider.SetControllerFromProgramArgs(), Times.Never);
            }
            finally
            {
                validateState.Dispose();
            }
        }

        [Fact]
        public void DirectConnection_UsesPersistentLocalIdentity()
        {
            var logic = new Mock<IClientLogic>();
            var controllerIdProvider = new Mock<IControllerIdProvider>();
            var coopFinalizer = new Mock<ICoopFinalizer>();
            var moduleInfoProvider = new Mock<IModuleInfoProvider>();
            moduleInfoProvider.Setup(provider => provider.GetModuleInfos()).Returns(Array.Empty<ModuleInfo>());
            ValidateModuleState validateState = null!;
            logic.SetupGet(client => client.State).Returns(() => validateState);

            validateState = new ValidateModuleState(
                logic.Object,
                clientComponent.TestMessageBroker,
                clientComponent.TestNetwork,
                controllerIdProvider.Object,
                coopFinalizer.Object,
                new Mock<IGameStateInterface>().Object,
                moduleInfoProvider.Object,
                new NetworkConfig { IsTunneled = false },
                new Mock<ISessionTransportTargetSource>().Object);

            try
            {
#if DEBUG
                controllerIdProvider.Verify(provider => provider.SetControllerFromProgramArgs(), Times.Once);
#else
                controllerIdProvider.Verify(provider => provider.SetControllerAsLocalId(), Times.Once);
#endif
                controllerIdProvider.Verify(
                    provider => provider.SetControllerAsPlatformIdentity(It.IsAny<PlatformIdentity>()),
                    Times.Never);
            }
            finally
            {
                validateState.Dispose();
            }
        }

        [Fact]
        public void PlayerOwnedProviderTunnel_RegistersLocalEndpointBeforeClaimingIdentity()
        {
            var logic = new Mock<IClientLogic>();
            var controllerIdProvider = new Mock<IControllerIdProvider>();
            var coopFinalizer = new Mock<ICoopFinalizer>();
            var moduleInfoProvider = new Mock<IModuleInfoProvider>();
            moduleInfoProvider.Setup(provider => provider.GetModuleInfos()).Returns(Array.Empty<ModuleInfo>());
            var transportTargetSource = new Mock<ISessionTransportTargetSource>();
            var identity = new PlatformIdentity("gog", "123456789");
            transportTargetSource.SetupGet(source => source.TunnelTarget).Returns(identity);
            var identityPublisher = new Mock<IPeerIdentityPublisher>();
            identityPublisher.SetupGet(publisher => publisher.IsAvailable).Returns(true);
            var localEndpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 43131);
            identityPublisher
                .Setup(publisher => publisher.TryRegister(localEndpoint, identity))
                .Returns(true);
            var localEndpointSource = new Mock<ILocalPeerEndpointSource>();
            localEndpointSource.SetupGet(source => source.LocalPeerEndpoint).Returns(localEndpoint);
            ValidateModuleState validateState = null!;
            logic.SetupGet(client => client.State).Returns(() => validateState);

            validateState = new ValidateModuleState(
                logic.Object,
                clientComponent.TestMessageBroker,
                clientComponent.TestNetwork,
                controllerIdProvider.Object,
                coopFinalizer.Object,
                new Mock<IGameStateInterface>().Object,
                moduleInfoProvider.Object,
                new NetworkConfig
                {
                    IsTunneled = false,
                    PeerIdentityBridgeName = PeerIdentityBridgeName.Create(),
                },
                transportTargetSource.Object,
                identityPublisher.Object,
                localEndpointSource.Object);

            try
            {
                identityPublisher.Verify(
                    publisher => publisher.TryRegister(localEndpoint, identity),
                    Times.Once);
                controllerIdProvider.Verify(
                    provider => provider.SetControllerAsPlatformIdentity(identity),
                    Times.Once);
                controllerIdProvider.Verify(provider => provider.SetControllerAsLocalId(), Times.Never);
            }
            finally
            {
                validateState.Dispose();
            }
        }

        [Fact]
        public void PlayerOwnedProviderTunnel_IdentityBridgeFailureStopsValidation()
        {
            var logic = new Mock<IClientLogic>();
            var controllerIdProvider = new Mock<IControllerIdProvider>();
            var coopFinalizer = new Mock<ICoopFinalizer>();
            var transportTargetSource = new Mock<ISessionTransportTargetSource>();
            transportTargetSource.SetupGet(source => source.TunnelTarget)
                .Returns(new PlatformIdentity("gog", "123456789"));
            var identityPublisher = new Mock<IPeerIdentityPublisher>();
            identityPublisher.SetupGet(publisher => publisher.IsAvailable).Returns(true);
            identityPublisher
                .Setup(publisher => publisher.TryRegister(
                    It.IsAny<System.Net.IPEndPoint>(),
                    It.IsAny<PlatformIdentity>()))
                .Returns(false);
            var localEndpointSource = new Mock<ILocalPeerEndpointSource>();
            localEndpointSource.SetupGet(source => source.LocalPeerEndpoint)
                .Returns(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 43132));
            ValidateModuleState validateState = null!;
            logic.SetupGet(client => client.State).Returns(() => validateState);

            validateState = new ValidateModuleState(
                logic.Object,
                clientComponent.TestMessageBroker,
                clientComponent.TestNetwork,
                controllerIdProvider.Object,
                coopFinalizer.Object,
                new Mock<IGameStateInterface>().Object,
                new Mock<IModuleInfoProvider>().Object,
                new NetworkConfig
                {
                    PeerIdentityBridgeName = PeerIdentityBridgeName.Create(),
                },
                transportTargetSource.Object,
                identityPublisher.Object,
                localEndpointSource.Object);

            try
            {
                DrainGameThread();

                coopFinalizer.Verify(
                    finalizer => finalizer.Finalize(It.Is<string>(reason =>
                        reason.Contains("persistent player identity"))),
                    Times.Once);
                controllerIdProvider.Verify(
                    provider => provider.SetControllerAsPlatformIdentity(It.IsAny<PlatformIdentity>()),
                    Times.Never);
            }
            finally
            {
                validateState.Dispose();
            }
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
        public void NetworkClientValidated_AfterTimeout_DoesNotTransition()
        {
            // Arrange — the timeout (game thread) wins the completion race and tears coop down.
            var validateState = clientLogic.SetState<ValidateModuleState>();
            validateState.TimeoutValidation();

            var payload = new MessagePayload<NetworkClientValidated>(
                this, new NetworkClientValidated(true, new Player("12345", "111", "12345", "12345", "12345")));

            // Act — the server's terminal validation response lands just after the timeout claimed
            // completion. It must observe the claim and no-op, NOT drive LoadSavedData forward.
            validateState.Handle_NetworkClientValidated(payload);

            // Assert — no forward transition (which in production would resolve ReceivingSavedDataState
            // from the container the timeout already tore down), and still exactly one teardown.
            Assert.IsType<ValidateModuleState>(clientLogic.State);
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

        private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);
    }
}
