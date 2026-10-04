using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Modules;
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
using System.Runtime.Serialization;
using System.Threading.Tasks;
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
        public void NetworkClientValidate_BannedSteamId_DisconnectsBeforePlayerResolution()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            const string steamId = "76561198000000042";
            var banList = serverComponent.Container.Resolve<Mock<ISteamBanList>>();
            banList.Setup(list => list.IsBanned(steamId)).Returns(true);
            var playerManager = serverComponent.Container.Resolve<Mock<IPlayerManager>>();

            currentState.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(
                playerPeer,
                new NetworkClientValidate(steamId)));

            banList.Verify(list => list.IsBanned(steamId), Times.Once);
            playerManager.Verify(
                manager => manager.TryGetPlayer(It.IsAny<string>(), out It.Ref<Player>.IsAny),
                Times.Never);
            var messages = serverComponent.TestNetwork.SentNetworkMessages
                .GetValueOrDefault(playerPeer.Id) ?? Enumerable.Empty<IMessage>();
            Assert.Empty(messages);
            Assert.IsType<ResolveCharacterState>(connectionLogic.State);
            Assert.Equal(ConnectionState.ShutdownRequested, playerPeer.ConnectionState);
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

            var herdAuthority = serverComponent.Container.Resolve<Mock<IHeadmanHerdQuestAuthority>>();
            playerManagerMock.Setup(value => value.RemovePlayer(player))
                .Callback(() => herdAuthority.Verify(value => value.CancelForPlayerRemoval(player.ControllerId), Times.Once))
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
        public void NetworkModuleVersionsValidate_IncompatibleBuild_ReportsOnceAndKeepsTheReply()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            var modules = new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) };
            SetServerModules(modules);

            currentState.Handle_ModuleVersionsValidate(new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules, "9.9.9+deadbeef")));

            var validated = Assert.IsType<NetworkModuleVersionsValidated>(
                Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer)));
            Assert.False(validated.Matches);
            Assert.Contains("the client uses '9.9.9+deadbeef'", validated.Reason);
            DenialLog.Verify(log => log.Report(
                IsPeer(playerPeer), JoinDenialKind.BuildMismatch, "9.9.9+deadbeef", validated.Reason), Times.Once);
            DenialLog.Verify(log => log.Report(
                It.IsAny<NetPeer>(), It.IsAny<JoinDenialKind>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_ModuleMismatch_ReportsModuleValidation()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            SetServerModules(new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) });

            currentState.Handle_ModuleVersionsValidate(new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer,
                new NetworkModuleVersionsValidate(
                    new List<ModuleInfo> { new ModuleInfo("MismatchedModule", false, false, new ApplicationVersion()) })));

            var validated = Assert.IsType<NetworkModuleVersionsValidated>(
                Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer)));
            Assert.False(validated.Matches);
            DenialLog.Verify(log => log.Report(
                IsPeer(playerPeer), JoinDenialKind.ModuleValidation, Common.ModInformation.BuildVersion, validated.Reason), Times.Once);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_GamePatchMismatch_ReportsModuleValidation()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            SetServerModules(new List<ModuleInfo>
            {
                new ModuleInfo("Native", true, false, new ApplicationVersion(ApplicationVersionType.Release, 1, 4, 8, 0)),
            });

            currentState.Handle_ModuleVersionsValidate(new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer,
                new NetworkModuleVersionsValidate(new List<ModuleInfo>
                {
                    new ModuleInfo("Native", true, false, new ApplicationVersion(ApplicationVersionType.Release, 1, 4, 9, 0)),
                })));

            DenialLog.Verify(log => log.Report(
                IsPeer(playerPeer),
                JoinDenialKind.ModuleValidation,
                Common.ModInformation.BuildVersion,
                It.Is<string>(reason => reason.StartsWith("Wrong game version detected."))), Times.Once);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_ModulesMatch_DoesNotReport()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            var modules = new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) };
            SetServerModules(modules);

            currentState.Handle_ModuleVersionsValidate(new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules)));

            DenialLog.Verify(log => log.Report(
                It.IsAny<NetPeer>(), It.IsAny<JoinDenialKind>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_OnlyUnsupportedCoop_DoesNotReport()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            SetServerModules(new List<ModuleInfo>());

            currentState.Handle_ModuleVersionsValidate(new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer,
                new NetworkModuleVersionsValidate(
                    new List<ModuleInfo> { new ModuleInfo("Coop", false, false, new ApplicationVersion()) })));

            // The reply is unchanged; the client joins on this exact reason.
            var validated = Assert.IsType<NetworkModuleVersionsValidated>(
                Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer)));
            Assert.False(validated.Matches);
            Assert.Equal(NetworkModuleVersionsValidated.UnsupportedCoopModuleReason, validated.Reason);
            DenialLog.Verify(log => log.Report(
                It.IsAny<NetPeer>(), It.IsAny<JoinDenialKind>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_UnsupportedCoopPlusAnotherModule_Reports()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            SetServerModules(new List<ModuleInfo>());

            currentState.Handle_ModuleVersionsValidate(new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer,
                new NetworkModuleVersionsValidate(new List<ModuleInfo>
                {
                    new ModuleInfo("Coop", false, false, new ApplicationVersion()),
                    new ModuleInfo("WarSails", false, false, new ApplicationVersion()),
                })));

            DenialLog.Verify(log => log.Report(
                IsPeer(playerPeer),
                JoinDenialKind.ModuleValidation,
                Common.ModInformation.BuildVersion,
                It.Is<string>(reason => reason.Contains("'Coop'") && reason.Contains("'WarSails'"))), Times.Once);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_TwoConnections_EachReportsOnlyItsOwnPeer()
        {
            var firstState = connectionLogic.SetState<ResolveCharacterState>();
            var secondLogic = serverComponent.Container.Resolve<ConnectionLogic>(
                new TypedParameter(typeof(NetPeer), differentPeer));
            var secondState = secondLogic.SetState<ResolveCharacterState>();
            var modules = new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) };
            SetServerModules(modules);

            var first = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules, "1.0.0+aaaa"));
            var second = new MessagePayload<NetworkModuleVersionsValidate>(
                differentPeer, new NetworkModuleVersionsValidate(modules, "2.0.0+bbbb"));

            // Every connection in this state receives every joiner's message.
            firstState.Handle_ModuleVersionsValidate(first);
            firstState.Handle_ModuleVersionsValidate(second);
            secondState.Handle_ModuleVersionsValidate(first);
            secondState.Handle_ModuleVersionsValidate(second);

            DenialLog.Verify(log => log.Report(
                IsPeer(playerPeer), JoinDenialKind.BuildMismatch, "1.0.0+aaaa", It.IsAny<string>()), Times.Once);
            DenialLog.Verify(log => log.Report(
                IsPeer(differentPeer), JoinDenialKind.BuildMismatch, "2.0.0+bbbb", It.IsAny<string>()), Times.Once);
            DenialLog.Verify(log => log.Report(
                It.IsAny<NetPeer>(), It.IsAny<JoinDenialKind>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public void NetworkModuleVersionsValidate_ReportThrows_StillSendsTheReply()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            var modules = new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) };
            SetServerModules(modules);
            DenialLog
                .Setup(log => log.Report(
                    It.IsAny<NetPeer>(), It.IsAny<JoinDenialKind>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("log failed"));

            currentState.Handle_ModuleVersionsValidate(new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules, "9.9.9+deadbeef")));

            var validated = Assert.IsType<NetworkModuleVersionsValidated>(
                Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer)));
            Assert.False(validated.Matches);
            Assert.Contains("Incompatible co-op mod build", validated.Reason);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_ValidationThrows_DoesNotReport()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            serverComponent.Container
                .Resolve<Mock<IModuleInfoProvider>>()
                .Setup(mip => mip.GetModuleInfos())
                .Throws(new InvalidOperationException("boom"));

            currentState.Handle_ModuleVersionsValidate(new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(new List<ModuleInfo>(), "9.9.9+deadbeef")));

            var validated = Assert.IsType<NetworkModuleVersionsValidated>(
                Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer)));
            Assert.Contains("failed to validate", validated.Reason);
            DenialLog.Verify(log => log.Report(
                It.IsAny<NetPeer>(), It.IsAny<JoinDenialKind>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_RepeatsOnOneConnection_ReportOnceThenTheCountOnLeave()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            var modules = new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) };
            SetServerModules(modules);
            var message = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules, "9.9.9+deadbeef"));

            currentState.Handle_ModuleVersionsValidate(message);
            currentState.Handle_ModuleVersionsValidate(message);
            currentState.Handle_ModuleVersionsValidate(message);

            // Every attempt still gets its reply.
            Assert.Equal(3, serverComponent.TestNetwork.GetPeerMessages(playerPeer).Count());
            DenialLog.Verify(log => log.Report(
                It.IsAny<NetPeer>(), It.IsAny<JoinDenialKind>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            DenialLog.Verify(log => log.ReportRepeats(It.IsAny<NetPeer>(), It.IsAny<int>()), Times.Never);

            connectionLogic.SetState<CreateCharacterState>();

            DenialLog.Verify(log => log.ReportRepeats(IsPeer(playerPeer), 3), Times.Once);
            DenialLog.Verify(log => log.ReportRepeats(It.IsAny<NetPeer>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void NetworkModuleVersionsValidate_ConcurrentRepeats_ReportOnceAndCountEvery()
        {
            var context = serverComponent.Container.Resolve<ConnectionContext>();
            var modules = new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) };
            SetServerModules(modules);
            // TestNetwork is not thread safe, so this state replies through a mock.
            var currentState = new ResolveCharacterState(
                connectionLogic,
                context.MessageBroker,
                Mock.Of<INetwork>(),
                context.ModuleValidator,
                context.PlayerManager,
                context.PlayerPartyRestorer,
                context.ObjectManager,
                context.ModuleInfoProvider,
                context.ExistingPlayerSender,
                context.SteamBanList,
                context.JoinValidationDenialLog,
                context.HerdQuestAuthority);
            var message = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules, "9.9.9+deadbeef"));

            Parallel.For(0, 1000, _ => currentState.Handle_ModuleVersionsValidate(message));
            currentState.Dispose();

            DenialLog.Verify(log => log.Report(
                It.IsAny<NetPeer>(), It.IsAny<JoinDenialKind>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            DenialLog.Verify(log => log.ReportRepeats(IsPeer(playerPeer), 1000), Times.Once);
        }

        [Fact]
        public void LeavingTheState_AfterOneDenial_DoesNotReportRepeats()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            var modules = new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) };
            SetServerModules(modules);

            currentState.Handle_ModuleVersionsValidate(new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules, "9.9.9+deadbeef")));
            connectionLogic.SetState<CreateCharacterState>();

            DenialLog.Verify(log => log.ReportRepeats(It.IsAny<NetPeer>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void LeavingTheState_WithoutDenial_DoesNotReportRepeats()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            var modules = new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) };
            SetServerModules(modules);
            var message = new MessagePayload<NetworkModuleVersionsValidate>(playerPeer, new NetworkModuleVersionsValidate(modules));

            currentState.Handle_ModuleVersionsValidate(message);
            currentState.Handle_ModuleVersionsValidate(message);
            connectionLogic.SetState<CreateCharacterState>();

            DenialLog.Verify(log => log.ReportRepeats(It.IsAny<NetPeer>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void LeavingTheState_ReportRepeatsThrows_StillChangesState()
        {
            var currentState = connectionLogic.SetState<ResolveCharacterState>();
            var modules = new List<ModuleInfo> { new ModuleInfo("1", false, false, new ApplicationVersion()) };
            SetServerModules(modules);
            DenialLog
                .Setup(log => log.ReportRepeats(It.IsAny<NetPeer>(), It.IsAny<int>()))
                .Throws(new InvalidOperationException("log failed"));
            var message = new MessagePayload<NetworkModuleVersionsValidate>(
                playerPeer, new NetworkModuleVersionsValidate(modules, "9.9.9+deadbeef"));

            currentState.Handle_ModuleVersionsValidate(message);
            currentState.Handle_ModuleVersionsValidate(message);
            connectionLogic.SetState<CreateCharacterState>();

            DenialLog.Verify(log => log.ReportRepeats(IsPeer(playerPeer), 2), Times.Once);
            Assert.IsType<CreateCharacterState>(connectionLogic.State);
        }

        private Mock<IJoinValidationDenialLog> DenialLog =>
            serverComponent.Container.Resolve<Mock<IJoinValidationDenialLog>>();

        // NetPeer compares by endpoint, and both test peers share one.
        private static NetPeer IsPeer(NetPeer peer) => It.Is<NetPeer>(candidate => ReferenceEquals(candidate, peer));

        private void SetServerModules(List<ModuleInfo> modules)
        {
            serverComponent.Container
                .Resolve<Mock<IModuleInfoProvider>>()
                .Setup(mip => mip.GetModuleInfos())
                .Returns(modules);
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
