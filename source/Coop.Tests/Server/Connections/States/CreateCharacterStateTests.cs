using Autofac;
using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Tests.Mocks;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Connections.States
{
    public class CreateCharacterStateTests
    {
        private const string TestCharacterObjectId = "CharacterObject_test";

        private readonly IConnectionLogic connectionLogic;
        private readonly NetPeer playerPeer;
        private readonly NetPeer differentPeer;
        private readonly ServerTestComponent serverComponent;

        public CreateCharacterStateTests(ITestOutputHelper output)
        {
            serverComponent = new ServerTestComponent(output);

            var container = serverComponent.Container;

            var network = container.Resolve<TestNetwork>();

            playerPeer = network.CreatePeer();
            differentPeer = network.CreatePeer();
            connectionLogic = container.Resolve<ConnectionLogic>(new NamedParameter("playerId", playerPeer));
        }

        [Fact]
        public void TransferSave_TransitionState_LoadingState()
        {
            connectionLogic.SetState<CreateCharacterState>();

            connectionLogic.TransferSave();

            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void UnusedStatesMethods_DoNothing()
        {
            connectionLogic.SetState<CreateCharacterState>();

            connectionLogic.CreateCharacter();
            connectionLogic.Load();
            connectionLogic.EnterCampaign();
            connectionLogic.EnterMission();

            Assert.IsType<CreateCharacterState>(connectionLogic.State);
        }

        [Fact]
        public void NetworkTransferNewHero_SendsHeroIdsToJoiningPeer()
        {
            // Arrange
            SetupUnpackedHero();
            var currentState = connectionLogic.SetState<CreateCharacterState>();

            // Act
            var payload = new MessagePayload<NetworkTransferNewHero>(
                playerPeer, new NetworkTransferNewHero("MyId", Array.Empty<byte>()));
            currentState.Handle_NetworkTransferNewHero(payload);

            // Assert — the joining peer is sent the server-assigned ids, then we send the save
            // (with the player-registry snapshot) and wait for the client to load (LoadingState).
            Assert.Single(serverComponent.TestNetwork.GetPeerMessagesFromType<NetworkHeroRecieved>(playerPeer));
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void NetworkTransferNewHero_BroadcastsNewHeroToOtherPeers()
        {
            // Arrange
            SetupUnpackedHero();
            var currentState = connectionLogic.SetState<CreateCharacterState>();

            // Act
            var payload = new MessagePayload<NetworkTransferNewHero>(
                playerPeer, new NetworkTransferNewHero("MyId", Array.Empty<byte>()));
            currentState.Handle_NetworkTransferNewHero(payload);

            // Assert — every other connected peer is told a new player hero was created, and the broadcast Player
            // carries the new player's CharacterObject id so other clients register it instead of falling back to
            // the default character object.
            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(differentPeer));
            var created = Assert.IsType<NetworkNewPlayerHeroCreated>(message);
            Assert.Equal(TestCharacterObjectId, created.Player.CharacterObjectId);
            Assert.IsType<LoadingState>(connectionLogic.State);
        }

        [Fact]
        public void NetworkTransferNewHero_UnregisteredCharacterObject_DoesNotBroadcast()
        {
            // Arrange — the hero's CharacterObject is not registered, so TryCreatePlayer must fail to resolve its id
            // (like a missing hero/party/clan) and nothing is broadcast.
            SetupUnpackedHero(registerCharacterObject: false);
            var currentState = connectionLogic.SetState<CreateCharacterState>();

            // Act
            var payload = new MessagePayload<NetworkTransferNewHero>(
                playerPeer, new NetworkTransferNewHero("MyId", Array.Empty<byte>()));
            currentState.Handle_NetworkTransferNewHero(payload);

            // Assert — the handler bails before broadcasting or advancing: the connection stays in
            // CreateCharacterState and nothing is sent to any peer (neither the broadcast to other peers nor the
            // id response to the joining peer).
            Assert.IsType<CreateCharacterState>(connectionLogic.State);
            Assert.Empty(serverComponent.TestNetwork.SentNetworkMessages);
        }

        /// <summary>
        /// Configures the mocked <see cref="IHeroInterface.ServerUnpackHero"/> to return a hero whose
        /// hero/party/clan/character-object are registered in the (real) object manager, so
        /// <see cref="CreateCharacterState.Handle_NetworkTransferNewHero"/> can resolve their ids instead of
        /// early-returning.
        /// </summary>
        /// <param name="registerCharacterObject">
        /// When false, the hero's CharacterObject is left unregistered so TryCreatePlayer fails to resolve its id.
        /// </param>
        private void SetupUnpackedHero(bool registerCharacterObject = true)
        {
            var objectManager = serverComponent.Container.Resolve<IObjectManager>();
            var heroInterfaceMock = serverComponent.Container.Resolve<Mock<IHeroInterface>>();
            var playerRegistryMock = serverComponent.Container.Resolve<Mock<IPlayerManager>>();

            // Construct stubs without running constructors (no live campaign required) and wire the hero to a party,
            // clan, and character object. CreateCharacterState.TryCreatePlayer resolves ids for all four.
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            var characterObject = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
            hero._partyBelongedTo = party;
            hero._clan = clan;
            hero._characterObject = characterObject;

            Assert.True(objectManager.AddExisting("Hero_test", hero));
            Assert.True(objectManager.AddExisting("MobileParty_test", party));
            Assert.True(objectManager.AddExisting("Clan_test", clan));
            if (registerCharacterObject)
                Assert.True(objectManager.AddExisting(TestCharacterObjectId, characterObject));

            heroInterfaceMock
                .Setup(h => h.ServerUnpackHero(It.IsAny<byte[]>()))
                .Returns(hero);
            playerRegistryMock
                .Setup(p => p.AddPlayer(It.IsAny<Player>()))
                .Returns(true);
        }
    }
}
