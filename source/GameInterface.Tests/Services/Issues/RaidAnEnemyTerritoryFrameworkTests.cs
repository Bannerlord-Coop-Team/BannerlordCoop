using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Moq;
using System;
using System.IO;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Tests.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public class RaidAnEnemyTerritoryFrameworkTests
{
    [Fact]
    public void AcceptCapture_PreservesTheQuestDeadline()
    {
        var source = UninitializedQuest();
        source.ChangeQuestDueTime(new CampaignTime(1234));

        var strategy = new RaidAnEnemyTerritoryQuestSolutionAcceptStrategy();
        var target = UninitializedQuest();
        strategy.Apply(IssueWith(target), strategy.Capture(IssueWith(source)));

        Assert.Equal(source.QuestDueTime.NumTicks, target.QuestDueTime.NumTicks);
    }

    [Fact]
    public void FinalizationProof_RejectsAnUnknownBranch()
    {
        var strategy = new RaidAnEnemyTerritoryFinalizationProofStrategy();
        Assert.False(strategy.TryRunBranch(UninitializedQuest(), 0));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProgressJournal_IsSentOnlyToTheConnectedOwner(bool connected)
    {
        var previousRole = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var issue = IssueWith(UninitializedQuest());
            var resolver = new Mock<IIssueResolver>();
            Hero giver = null;
            IQuestTypeDescriptor descriptor = null;
            resolver.Setup(value => value.TryResolve("giver", "issue", out giver, out issue, out descriptor)).Returns(true);
            var owners = new Mock<IIssueOwnerResolver>();
            var owner = "owner";
            owners.Setup(value => value.TryGetOwnerControllerId(issue, out owner)).Returns(true);
            var players = new Mock<IPlayerManager>();
            var peer = (NetPeer)RuntimeHelpers.GetUninitializedObject(typeof(NetPeer));
            players.Setup(value => value.TryGetPeer(owner, out peer)).Returns(connected);
            var network = new Mock<INetwork>();
            var broker = new Mock<IMessageBroker>();
            Action<MessagePayload<RaidAnEnemyTerritoryProgressReady>> receive = null;
            broker.Setup(value => value.Subscribe(It.IsAny<Action<MessagePayload<RaidAnEnemyTerritoryProgressReady>>>()))
                .Callback<Action<MessagePayload<RaidAnEnemyTerritoryProgressReady>>>(value => receive = value);
            using var handler = new RaidAnEnemyTerritoryProgressHandler(broker.Object, network.Object,
                Mock.Of<IObjectManager>(), resolver.Object, owners.Object, players.Object, Mock.Of<IControllerIdProvider>());

            receive(new MessagePayload<RaidAnEnemyTerritoryProgressReady>(this,
                new RaidAnEnemyTerritoryProgressReady("giver", "issue", Array.Empty<string>(), Array.Empty<RaidAnEnemyTerritoryJournalEntry>())));

            network.Verify(value => value.Send(peer, It.Is<IMessage>(message => message is NetworkRaidAnEnemyTerritoryProgress)),
                connected ? Times.Once() : Times.Never());
            network.Verify(value => value.SendAll(It.IsAny<IMessage>()), Times.Never());
            network.VerifyNoOtherCalls();
        }
        finally
        {
            ModInformation.IsServer = previousRole;
        }
    }

    [Fact]
    public void ProgressReplay_DoesNotChangeAnotherPlayersLoadedQuest()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
        var previousRole = ModInformation.IsServer;
        ModInformation.IsServer = false;
        try
        {
            var quest = UninitializedQuest();
            // The skipped constructor normally initializes this readonly list.
            HarmonyLib.AccessTools.Field(typeof(Quest), nameof(Quest._raidedVillages)).SetValue(quest,
                new List<Settlement> { (Settlement)RuntimeHelpers.GetUninitializedObject(typeof(Settlement)) });
            var issue = IssueWith(quest);
            var resolver = new Mock<IIssueResolver>();
            Hero giver = null;
            IQuestTypeDescriptor descriptor = null;
            resolver.Setup(value => value.TryResolve("giver", "issue", out giver, out issue, out descriptor)).Returns(true);
            var owners = new Mock<IIssueOwnerResolver>();
            var owner = "owner";
            owners.Setup(value => value.TryGetOwnerControllerId(issue, out owner)).Returns(true);
            var controller = new Mock<IControllerIdProvider>();
            controller.SetupGet(value => value.ControllerId).Returns("bystander");
            var broker = new Mock<IMessageBroker>();
            Action<MessagePayload<NetworkRaidAnEnemyTerritoryProgress>> receive = null;
            broker.Setup(value => value.Subscribe(It.IsAny<Action<MessagePayload<NetworkRaidAnEnemyTerritoryProgress>>>()))
                .Callback<Action<MessagePayload<NetworkRaidAnEnemyTerritoryProgress>>>(value => receive = value);
            using var handler = new RaidAnEnemyTerritoryProgressHandler(broker.Object, Mock.Of<INetwork>(),
                Mock.Of<IObjectManager>(), resolver.Object, owners.Object, Mock.Of<IPlayerManager>(), controller.Object);

            GameThread.Run(() => receive(new MessagePayload<NetworkRaidAnEnemyTerritoryProgress>(this,
                new NetworkRaidAnEnemyTerritoryProgress("giver", "issue", Array.Empty<string>(), Array.Empty<RaidAnEnemyTerritoryJournalEntry>()))), blocking: true);

            Assert.Single(quest._raidedVillages);
        }
        finally
        {
            ModInformation.IsServer = previousRole;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void AcceptanceRejectsATruncatedDeadline(int length)
    {
        var strategy = new RaidAnEnemyTerritoryQuestSolutionAcceptStrategy();
        Assert.Throws<InvalidDataException>(() => strategy.Apply(IssueWith(UninitializedQuest()), new byte[length]));
    }

    [Fact]
    public void CreationRejectsATruncatedKingdomBeforeConstructingAnIssue()
    {
        var strategy = new RaidAnEnemyTerritoryCreationCaptureStrategy(Mock.Of<IObjectManager>());
        Assert.Throws<InvalidDataException>(() => strategy.CreateIssue(null, new byte[] { 3, 65 }));
    }

    private static IssueBase IssueWith(Quest quest)
    {
        var issue = (IssueBase)RuntimeHelpers.GetUninitializedObject(typeof(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue));
        issue.IssueQuest = quest;
        return issue;
    }

    private static Quest UninitializedQuest() =>
        (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
}
