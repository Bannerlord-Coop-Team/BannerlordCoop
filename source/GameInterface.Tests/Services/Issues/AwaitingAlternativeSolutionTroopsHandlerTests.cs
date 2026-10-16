using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using Moq;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

/// <summary>Checks that client requests cannot create an alternative mission's return entitlement.</summary>
[Collection(ModInformationRoleCollection.Name)]
public class AwaitingAlternativeSolutionTroopsHandlerTests
{
    [Fact]
    public void RegisteredPlayerDepositThenDrain_DoesNotCreateOrAwardPendingTroops()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            using var broker = new MessageBroker();
            using var peers = new TestNetwork();
            var peer = peers.CreatePeer();
            var player = new Player("owner", "hero", "party", "clan", "character");
            var players = new Mock<IPlayerManager>();
            players.Setup(x => x.TryGetPlayer(peer, out player)).Returns(true);
            var network = new Mock<INetwork>();
            var objects = new Mock<IObjectManager>(MockBehavior.Strict);
            var rosters = new Mock<ITroopRosterInterface>(MockBehavior.Strict);
            var pending = new Mock<IAwaitingAlternativeSolutionTroopsRegistry>();
            using var handler = new AwaitingAlternativeSolutionTroopsHandler(
                broker, network.Object, objects.Object, rosters.Object, players.Object, pending.Object);
            var troops = new TroopRosterData(Array.Empty<TroopRosterElementData>());

            broker.Publish(peer, new RequestAwaitingAlternativeSolutionTroopsDeposit("giver", troops));
            broker.Publish(peer, new RequestAwaitingAlternativeSolutionTroopsDrain(troops));
            GameThread.Run(() => { }, blocking: true);

            network.Verify(x => x.Send(peer, It.IsAny<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>()), Times.Once);
            network.Verify(x => x.Send(peer, It.IsAny<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>()), Times.Never);
            network.Verify(x => x.Send(peer, It.IsAny<NetworkAwaitingAlternativeSolutionTroopsDrainResult>()), Times.Once);
            pending.Verify(x => x.Deposit(It.IsAny<string>(), It.IsAny<TroopRoster>()), Times.Never);
            objects.VerifyNoOtherCalls();
            rosters.VerifyNoOtherCalls();
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
