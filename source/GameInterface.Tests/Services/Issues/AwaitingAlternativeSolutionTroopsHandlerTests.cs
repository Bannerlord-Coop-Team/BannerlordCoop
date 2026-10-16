using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Services.Issues.Generic;
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
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public class AwaitingAlternativeSolutionTroopsHandlerTests
{
    [Fact]
    public void OwnerDepositWhileMissionIsRunning_PreservesSentRosterAndExistingReturnQueue()
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
            var giver = ObjectHelper.SkipConstructor<Hero>();
            var issue = ObjectHelper.SkipConstructor<GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue>();
            giver.Issue = issue;
            issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.Days(1000000);
            typeof(IssueBase).GetField(nameof(IssueBase.AlternativeSolutionSentTroops))!.SetValue(issue, new TroopRoster());
            var sent = ObjectHelper.SkipConstructor<CharacterObject>();
            issue.AlternativeSolutionSentTroops.AddToCounts(sent, 6);
            var objects = new Mock<IObjectManager>();
            objects.Setup(x => x.TryGetObjectWithLogging("giver", out giver)).Returns(true);
            var generations = new Mock<IIssueGenerationRegistry>();
            int generation = 7;
            generations.Setup(x => x.TryGetGeneration(giver, out generation)).Returns(true);
            var network = new Mock<INetwork>();
            var rosters = new Mock<ITroopRosterInterface>(MockBehavior.Strict);
            var pending = new AwaitingAlternativeSolutionTroopsRegistry();
            var prior = new TroopRoster();
            var returned = ObjectHelper.SkipConstructor<CharacterObject>();
            prior.AddToCounts(returned, 2);
            pending.Deposit(player.ControllerId, prior);
            using var handler = new AwaitingAlternativeSolutionTroopsHandler(broker, network.Object,
                objects.Object, rosters.Object, players.Object, generations.Object, pending);

            var claimed = new TroopRosterData(new[] { new TroopRosterElementData(1, 6, 0, 0) });
            broker.Publish(peer, new RequestAwaitingAlternativeSolutionTroopsDeposit("giver", claimed));
            GameThread.Run(() => { }, blocking: true);

            network.Verify(x => x.Send(peer, It.IsAny<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>()), Times.Once);
            network.Verify(x => x.Send(peer, It.IsAny<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>()), Times.Never);
            Assert.True(pending.TryGet(player.ControllerId, out var remaining));
            Assert.Equal(2, remaining.TotalManCount);
            Assert.Equal(0, remaining.GetTroopCount(sent));
            Assert.Equal(6, issue.AlternativeSolutionSentTroops.GetTroopCount(sent));
            Assert.True(issue.IsSolvingWithAlternative);
            rosters.VerifyNoOtherCalls();
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
