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
        public void TransferCharacter_TransitionState_TransferCharacterState()
        {
            connectionLogic.SetState<CreateCharacterState>();

            connectionLogic.TransferSave();

            Assert.IsType<TransferSaveState>(connectionLogic.State);
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

            // Assert — the joining peer is sent the server-assigned ids, then we move on to the save transfer
            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(playerPeer));
            Assert.IsType<NetworkHeroRecieved>(message);
            Assert.IsType<TransferSaveState>(connectionLogic.State);
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

            // Assert — every other connected peer is told a new player hero was created
            var message = Assert.Single(serverComponent.TestNetwork.GetPeerMessages(differentPeer));
            Assert.IsType<NetworkNewPlayerHeroCreated>(message);
            Assert.IsType<TransferSaveState>(connectionLogic.State);
        }

        /// <summary>
        /// Configures the mocked <see cref="IHeroInterface.UnpackHero"/> to return a hero whose hero/party are
        /// registered in the (real) object manager, so <see cref="CreateCharacterState.Handle_NetworkTransferNewHero"/>
        /// can resolve their ids instead of early-returning.
        /// </summary>
        private void SetupUnpackedHero()
        {
            var objectManager = serverComponent.Container.Resolve<IObjectManager>();
            var heroInterfaceMock = serverComponent.Container.Resolve<Mock<IHeroInterface>>();
            var playerRegistryMock = serverComponent.Container.Resolve<Mock<IPlayerManager>>();

            // Construct stubs without running constructors (no live campaign required) and wire the hero to a party.
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            hero._partyBelongedTo = party;

            Assert.True(objectManager.AddExisting("Hero_test", hero));
            Assert.True(objectManager.AddExisting("MobileParty_test", party));

            heroInterfaceMock
                .Setup(h => h.UnpackHero(It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns(hero);
            playerRegistryMock
                .Setup(p => p.AddPlayer(It.IsAny<Player>()))
                .Returns(true);
        }
    }
}
