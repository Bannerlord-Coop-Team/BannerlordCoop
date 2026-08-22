using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class ResolveCharacterStateTests
    {
        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        public ResolveCharacterStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new TypedParameter(typeof(NetPeer), playerPeer));
        }

        [Fact]
        public void CreateCharacterMethod_TransitionState_CreateCharacterState()
        {
            // Arrange
            connectionLogic.SetState<ResolveCharacterState>();

            // Act
            connectionLogic.CreateCharacter();

            // Assert
            Assert.IsType<CreateCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void TransferSaveMethod_TransitionState_LoadingState()
        {
            // Arrange
            connectionLogic.SetState<ResolveCharacterState>();

            // Act
            connectionLogic.TransferSave();

            // Assert — TransferSave sends the save (TransferSaveState) then immediately advances to
            // LoadingState to await the client entering the campaign.
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            // Arrange
            connectionLogic.SetState<ResolveCharacterState>();

            // Act
            connectionLogic.Load();
            connectionLogic.EnterCampaign();
            connectionLogic.EnterMission();

            // Assert
            Assert.IsType<ResolveCharacterState>(connectionLogic.State);
        }
        
        [Fact]
        public void NetworkModuleVersionsValidate_ModulesMatches()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            // Community (non-official) modules — official modules are exempt from module
            // matching, so they would not exercise the comparison at all.
            var modules = new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) };

            serverComponent.Container
                .Resolve<Mock<IModuleInfoProvider>>()
                .Setup(mip => mip.GetModuleInfos())
                .Returns(modules);

            // Act
            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules));
            currentState.Handle_ModuleVersionsValidate(payload);

            // Assert
            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer));
            Assert.IsType<NetworkModuleVersionsValidated>(message);

            var castedMessage = (NetworkModuleVersionsValidated)message;
            Assert.True(castedMessage.Matches);
            Assert.Equal(Common.ModInformation.BuildVersion, castedMessage.CoopBuildVersion);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("different-build")]
        public void NetworkModuleVersionsValidate_IncompatibleBuild_Denied(string? clientBuildVersion)
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            var modules = new List<ModuleInfo>
            {
                new ModuleInfo("1", false, false, new ApplicationVersion()),
            };
            serverComponent.Container
                .Resolve<Mock<IModuleInfoProvider>>()
                .Setup(mip => mip.GetModuleInfos())
                .Returns(modules);

            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer,
                new NetworkModuleVersionsValidate(modules, clientBuildVersion));
            currentState.Handle_ModuleVersionsValidate(payload);

            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer));
            var validated = Assert.IsType<NetworkModuleVersionsValidated>(message);
            Assert.False(validated.Matches);
            Assert.Contains("Incompatible co-op mod build", validated.Reason);
            Assert.Contains("Update the co-op mod on both sides", validated.Reason);
            Assert.Equal(Common.ModInformation.BuildVersion, validated.CoopBuildVersion);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_ProtobufRoundTrip_PreservesBuildVersion()
        {
            var message = new NetworkModuleVersionsValidate(Array.Empty<ModuleInfo>(), "client-build");

            var deserialized = ProtobufRoundTrip(message);

            Assert.Equal("client-build", deserialized.CoopBuildVersion);
        }

        [Fact]
        public void NetworkModuleVersionsValidated_ProtobufRoundTrip_PreservesBuildVersion()
        {
            var message = new NetworkModuleVersionsValidated(false, "reason", "server-build");

            var deserialized = ProtobufRoundTrip(message);

            Assert.Equal("server-build", deserialized.CoopBuildVersion);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_ModulesMismatch()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            // Community (non-official) modules — official modules are exempt from module
            // matching (a dedicated server's official module set differs from a client's),
            // so only community modules can produce a mismatch.
            serverComponent.Container
                .Resolve<Mock<IModuleInfoProvider>>()
                .Setup(mip => mip.GetModuleInfos())
                .Returns(
                    new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) }
                );

            // Act
            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(new List<ModuleInfo> { new ModuleInfo("MismatchedModule", false, false, new ApplicationVersion())}));
            currentState.Handle_ModuleVersionsValidate(payload);

            // Assert
            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer));
            Assert.IsType<NetworkModuleVersionsValidated>(message);

            var castedMessage = (NetworkModuleVersionsValidated)message;
            Assert.False(castedMessage.Matches);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_FromDifferentPeer_Ignored()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            var modules = new List<ModuleInfo> { new ModuleInfo("1", true, false, new ApplicationVersion()) };

            serverComponent.Container
                .Resolve<Mock<IModuleInfoProvider>>()
                .Setup(mip => mip.GetModuleInfos())
                .Returns(modules);

            // Act — another connection's validate request must not be answered by this connection;
            // without the peer guard every concurrent joiner was also answered with a result
            // computed from another client's module list.
            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                differentPeer, new NetworkModuleVersionsValidate(modules));
            currentState.Handle_ModuleVersionsValidate(payload);

            // Assert — no response was sent to anyone.
            Assert.Empty(serverComponent.TestNetwork.SentNetworkMessages);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_ValidationThrows_RespondsDenied()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            serverComponent.Container
                .Resolve<Mock<IModuleInfoProvider>>()
                .Setup(mip => mip.GetModuleInfos())
                .Throws(new System.InvalidOperationException("boom"));

            // Act — a throw used to die in the network poller, so the joiner never got an answer
            // and sat on the "Validating modules..." loading screen forever.
            var payload = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(new List<ModuleInfo>()));
            currentState.Handle_ModuleVersionsValidate(payload);

            // Assert — the client must receive a denial with a reason instead of silence.
            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer));
            var castedMessage = Assert.IsType<NetworkModuleVersionsValidated>(message);
            Assert.False(castedMessage.Matches);
            Assert.Contains("failed to validate", castedMessage.Reason);
        }

        [Fact]
        public void NetworkClientValidate_ValidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            

            var player = new Player("MyPlayer", "MyHero", "MyParty", "MyClan", "MyCharacter");

            var playerManagerMock = serverComponent.Container.Resolve<Mock<IPlayerManager>>();

            playerManagerMock
                .Setup(i => i.TryGetPlayer(player.ControllerId, out It.Ref<Player>.IsAny))
                .Callback((string id, out Player returnedPlayer) =>
                {
                    returnedPlayer = player;
                })
                .Returns(true);

            var objectManager = serverComponent.Container.Resolve<IObjectManager>();
            var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            Assert.True(objectManager.AddExisting(player.HeroId, hero));
            var restoredPlayer = player;

            serverComponent.Container
                .Resolve<Mock<IPlayerPartyRestorer>>()
                .Setup(restorer => restorer.TryRestore(player, out restoredPlayer))
                .Returns(true);

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                playerPeer, new NetworkClientValidate(player.ControllerId));
            currentState.Handle_ClientValidate(payload);

            // Assert
            var messages = serverComponent.TestNetwork.SentNetworkMessages[playerPeer.Id];

            var validated = messages.OfType<NetworkClientValidated>();

            var message = Assert.Single(validated);

            Assert.True(message.HeroExists);
            Assert.Equal(player, message.Player);
        }

        [Fact]
        public void NetworkClientValidate_RegisteredHeroWithStaleParty_RepairsWithoutCreatingCharacter()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            var player = new Player("MyPlayer", "MyHero", "MissingParty", "MyClan", "MyCharacter");
            var repaired = new Player("MyPlayer", "MyHero", "RecoveredParty", "MyClan", "MyCharacter");
            var playerManager = serverComponent.Container.Resolve<Mock<IPlayerManager>>();
            var registeredPlayer = player;
            playerManager
                .Setup(manager => manager.TryGetPlayer(player.ControllerId, out registeredPlayer))
                .Returns(true);
            playerManager.Setup(manager => manager.ReplacePlayer(player, repaired)).Returns(true);

            var objectManager = serverComponent.Container.Resolve<IObjectManager>();
            var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            Assert.True(objectManager.AddExisting(player.HeroId, hero));
            var restoredPlayer = repaired;

            serverComponent.Container
                .Resolve<Mock<IPlayerPartyRestorer>>()
                .Setup(restorer => restorer.TryRestore(player, out restoredPlayer))
                .Returns(true);

            currentState.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(
                playerPeer,
                new NetworkClientValidate(player.ControllerId)));

            playerManager.Verify(manager => manager.ReplacePlayer(player, repaired), Times.Once);
            playerManager.Verify(manager => manager.RemovePlayer(It.IsAny<Player>()), Times.Never);

            var validation = Assert.Single(
                serverComponent.TestNetwork.GetPeerMessages(playerPeer).OfType<NetworkClientValidated>());
            Assert.True(validation.HeroExists);
            Assert.Same(repaired, validation.Player);

            var update = Assert.Single(
                serverComponent.TestNetwork.GetPeerMessages(differentPeer)
                    .OfType<NetworkPlayerRegistrationUpdated>());
            Assert.Same(repaired, update.Player);
            Assert.IsNotType<CreateCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void NetworkClientValidate_RegisteredPlayerWithMissingHero_DropsStaleRegistration()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            // Registered, but its hero is never added to the object manager — the shape a save
            // carries when a registration outlives the objects it names.
            var player = new Player("MyPlayer", "MissingHero", "MyParty", "MyClan", "MyCharacter");

            var playerManagerMock = serverComponent.Container.Resolve<Mock<IPlayerManager>>();
            playerManagerMock
                .Setup(i => i.TryGetPlayer(player.ControllerId, out It.Ref<Player>.IsAny))
                .Callback((string id, out Player returnedPlayer) =>
                {
                    returnedPlayer = player;
                })
                .Returns(true);

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                playerPeer, new NetworkClientValidate(player.ControllerId));
            currentState.Handle_ClientValidate(payload);

            // Assert — the dead registration must be dropped before character creation, otherwise
            // the character created next registers this controller a second time and every later
            // lookup for it is ambiguous.
            playerManagerMock.Verify(i => i.RemovePlayer(player), Times.Once);
            playerManagerMock.Verify(i => i.SetPeer(It.IsAny<string>(), It.IsAny<NetPeer>()), Times.Never);

            var message = Assert.Single(
                serverComponent.TestNetwork.GetPeerMessages(playerPeer).OfType<NetworkClientValidated>());
            Assert.False(message.HeroExists);

            Assert.IsType<CreateCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void NetworkClientValidate_ResolutionThrows_DoesNotAnswerOrAdvance()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            const string playerId = "MyPlayer";
            serverComponent.Container
                .Resolve<Mock<IPlayerManager>>()
                .Setup(i => i.TryGetPlayer(playerId, out It.Ref<Player>.IsAny))
                .Throws(new InvalidOperationException("boom"));

            // Act — a throw used to escape into the network poller, so the joiner got no reply at
            // all and sat on the validation screen until its 30s deadline expired.
            var payload = new MessagePayload<NetworkClientValidate>(
                playerPeer, new NetworkClientValidate(playerId));
            currentState.Handle_ClientValidate(payload);

            // Assert — the connection is dropped rather than answered: NetworkClientValidated
            // carries no reason, and answering "no hero" would push the player into creating a
            // second character.
            var messages = serverComponent.TestNetwork.SentNetworkMessages
                .GetValueOrDefault(playerPeer.Id) ?? Enumerable.Empty<IMessage>();
            Assert.Empty(messages.OfType<NetworkClientValidated>());

            Assert.IsType<ResolveCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void NetworkClientValidate_InvalidPlayerId()
        {
            // Arrange
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            string playerId = "MyPlayer";

            serverComponent.Container
                .Resolve<Mock<IPlayerManager>>()
                .Setup(i => i.TryGetPlayer(playerId, out It.Ref<Player>.IsAny))
                .Callback((string id, out Player? returnedPlayer) =>
                {
                    returnedPlayer = null;
                })
                .Returns(false);

            // Act
            var payload = new MessagePayload<NetworkClientValidate>(
                differentPeer, new NetworkClientValidate(playerId));
            currentState.Handle_ClientValidate(payload);

            // Assert
            var messages = serverComponent.TestNetwork.SentNetworkMessages
                .GetValueOrDefault(playerPeer.Id) ?? Enumerable.Empty<IMessage>();

            Assert.Empty(messages.OfType<NetworkClientValidated>());
        }

        [Fact]
        public void NetworkClientValidate_DirectLocalIdentityBindsControllerIdToConnection()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();

            currentState.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(
                playerPeer,
                new NetworkClientValidate("local:installation-id")));

            Assert.Equal("local:installation-id", connectionLogic.ControllerId);
            Assert.IsType<CreateCharacterState>(connectionLogic.State);
        }

        [Theory]
        [InlineData("steam", "76561198000000001")]
        [InlineData("gog", "123456789")]
        public void NetworkClientValidate_AuthenticatedTransportIdentityBindsMatchingControllerId(
            string providerName,
            string platformUserId)
        {
            var authenticatedIdentity = new PlatformIdentity(providerName, platformUserId);
            var identityResolver = CreateIdentityResolver(authenticatedIdentity);
            var currentState = CreateState(identityResolver.Object);

            try
            {
                currentState.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(
                    playerPeer,
                    new NetworkClientValidate(authenticatedIdentity.ControllerId)));

                Assert.Equal(authenticatedIdentity.ControllerId, connectionLogic.ControllerId);
                Assert.IsType<CreateCharacterState>(connectionLogic.State);
            }
            finally
            {
                currentState.Dispose();
            }
        }

        [Theory]
        [InlineData("steam", "76561198000000001")]
        [InlineData("gog", "123456789")]
        public void NetworkClientValidate_AuthenticatedIdentityMigratesMatchingLegacyRegistration(
            string providerName,
            string platformUserId)
        {
            var authenticatedIdentity = new PlatformIdentity(providerName, platformUserId);
            var identityResolver = CreateIdentityResolver(authenticatedIdentity);
            var currentState = CreateState(identityResolver.Object);
            var playerManager = serverComponent.Container.Resolve<Mock<IPlayerManager>>();
            var legacyPlayer = new Player(
                platformUserId,
                "LegacyHero",
                string.Empty,
                string.Empty,
                string.Empty);
            var migratedPlayer = new Player(
                authenticatedIdentity.ControllerId,
                "LegacyHero",
                string.Empty,
                string.Empty,
                string.Empty);
            var objectManager = serverComponent.Container.Resolve<IObjectManager>();
            var legacyHero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            Assert.True(objectManager.AddExisting(migratedPlayer.HeroId, legacyHero));

            playerManager
                .Setup(manager => manager.TryGetPlayer(platformUserId, out legacyPlayer))
                .Returns(true);
            playerManager
                .Setup(manager => manager.TryMigrateControllerId(
                    platformUserId,
                    authenticatedIdentity.ControllerId,
                    out migratedPlayer))
                .Returns(true);

            try
            {
                currentState.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(
                    playerPeer,
                    new NetworkClientValidate(
                        authenticatedIdentity.ControllerId,
                        platformUserId)));

                playerManager.Verify(manager => manager.TryMigrateControllerId(
                    platformUserId,
                    authenticatedIdentity.ControllerId,
                    out migratedPlayer), Times.Once);
                Assert.Equal(authenticatedIdentity.ControllerId, connectionLogic.ControllerId);
            }
            finally
            {
                currentState.Dispose();
            }
        }

        [Fact]
        public void NetworkClientValidate_DirectLocalIdentityMigratesLegacyNumericRegistration()
        {
            const string legacyControllerId = "76561198000000001";
            const string controllerId = "local:installation-id";
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            var playerManager = serverComponent.Container.Resolve<Mock<IPlayerManager>>();
            var legacyPlayer = new Player(
                legacyControllerId,
                "LegacyHero",
                string.Empty,
                string.Empty,
                string.Empty);
            var migratedPlayer = new Player(
                controllerId,
                "LegacyHero",
                string.Empty,
                string.Empty,
                string.Empty);
            var objectManager = serverComponent.Container.Resolve<IObjectManager>();
            var legacyHero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            Assert.True(objectManager.AddExisting(migratedPlayer.HeroId, legacyHero));

            playerManager
                .Setup(manager => manager.TryGetPlayer(legacyControllerId, out legacyPlayer))
                .Returns(true);
            playerManager
                .Setup(manager => manager.TryMigrateControllerId(
                    legacyControllerId,
                    controllerId,
                    out migratedPlayer))
                .Returns(true);

            currentState.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(
                playerPeer,
                new NetworkClientValidate(controllerId, legacyControllerId)));

            playerManager.Verify(manager => manager.TryMigrateControllerId(
                legacyControllerId,
                controllerId,
                out migratedPlayer), Times.Once);
            Assert.Equal(controllerId, connectionLogic.ControllerId);
        }

        [Theory]
        [InlineData("steam", "76561198000000001", "76561198000000002")]
        [InlineData("gog", "123456789", "987654321")]
        public void NetworkClientValidate_RejectsSpoofedStorefrontUserId(
            string providerName,
            string authenticatedUserId,
            string claimedUserId)
        {
            var identityResolver = CreateIdentityResolver(
                new PlatformIdentity(providerName, authenticatedUserId));
            var currentState = CreateState(identityResolver.Object);

            try
            {
                currentState.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(
                    playerPeer,
                    new NetworkClientValidate(new PlatformIdentity(providerName, claimedUserId).ControllerId)));

                Assert.Null(connectionLogic.ControllerId);
                Assert.IsType<ResolveCharacterState>(connectionLogic.State);
                serverComponent.Container.Resolve<Mock<IPlayerManager>>().Verify(
                    manager => manager.TryGetPlayer(It.IsAny<string>(), out It.Ref<Player>.IsAny),
                    Times.Never);
            }
            finally
            {
                currentState.Dispose();
            }
        }

        [Theory]
        [InlineData("steam", "gog")]
        [InlineData("gog", "steam")]
        public void NetworkClientValidate_RejectsOtherStorefrontWithSameNumericId(
            string authenticatedProvider,
            string claimedProvider)
        {
            const string platformUserId = "123456789";
            var identityResolver = CreateIdentityResolver(
                new PlatformIdentity(authenticatedProvider, platformUserId));
            var currentState = CreateState(identityResolver.Object);

            try
            {
                currentState.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(
                    playerPeer,
                    new NetworkClientValidate(
                        new PlatformIdentity(claimedProvider, platformUserId).ControllerId)));

                Assert.Null(connectionLogic.ControllerId);
                Assert.IsType<ResolveCharacterState>(connectionLogic.State);
            }
            finally
            {
                currentState.Dispose();
            }
        }

        [Theory]
        [InlineData("steam:76561198000000001")]
        [InlineData("gog:123456789")]
        public void NetworkClientValidate_RejectsUnauthenticatedStorefrontClaim(string controllerId)
        {
            var identityResolver = new Mock<IAuthenticatedPeerIdentityResolver>();
            var currentState = CreateState(identityResolver.Object);

            try
            {
                currentState.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(
                    playerPeer,
                    new NetworkClientValidate(controllerId)));

                Assert.Null(connectionLogic.ControllerId);
                Assert.IsType<ResolveCharacterState>(connectionLogic.State);
            }
            finally
            {
                currentState.Dispose();
            }
        }

        private Mock<IAuthenticatedPeerIdentityResolver> CreateIdentityResolver(
            PlatformIdentity authenticatedIdentity)
        {
            var resolver = new Mock<IAuthenticatedPeerIdentityResolver>();
            resolver
                .Setup(candidate => candidate.TryGetIdentity(
                    It.Is<IPEndPoint>(endpoint =>
                        endpoint.Address.Equals(playerPeer.Address) && endpoint.Port == playerPeer.Port),
                    out authenticatedIdentity))
                .Returns(true);
            return resolver;
        }

        private ResolveCharacterState CreateState(IAuthenticatedPeerIdentityResolver identityResolver)
        {
            return new ResolveCharacterState(
                connectionLogic,
                serverComponent.Container.Resolve<IMessageBroker>(),
                serverComponent.Container.Resolve<INetwork>(),
                serverComponent.Container.Resolve<IModuleValidator>(),
                serverComponent.Container.Resolve<IPlayerManager>(),
                serverComponent.Container.Resolve<IPlayerPartyRestorer>(),
                serverComponent.Container.Resolve<IObjectManager>(),
                serverComponent.Container.Resolve<IModuleInfoProvider>(),
                serverComponent.Container.Resolve<IExistingPlayerSender>(),
                identityResolver);
        }

        private static T ProtobufRoundTrip<T>(T message)
        {
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, message);
            stream.Position = 0;
            return Serializer.Deserialize<T>(stream);
        }
    }
}
