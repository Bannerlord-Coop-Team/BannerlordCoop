using Autofac;
using GameInterface.Services.Issues;
using GameInterface.Services.Players;
using Common.Util;
using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.IO;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt;
using Moq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;
using RevoltBattleEnd = GameInterface.Services.Issues.Quests.LesserNobleRevolt.LesserNobleRevoltWorldEventService.RevoltBattleEnd;

namespace GameInterface.Tests.Services.Issues;

public class LesserNobleRevoltFrameworkTests
{
    [Fact]
    public void AcceptCapture_RestoresDueTimeWhenTheServerPartyIsNotInTheClientCapture()
    {
        var issue = ObjectHelper.SkipConstructor<Issue>();
        var quest = ObjectHelper.SkipConstructor<TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest>();
        quest._suitableVillagesToVisitList = new List<Settlement>();
        quest._visitedVillagesList = new List<Settlement>();
        quest.QuestDueTime = CampaignTime.Days(12f);
        issue.IssueQuest = quest;

        var strategy = new LesserNobleRevoltQuestSolutionAcceptStrategy(
            new Mock<IObjectManager>().Object,
            new Mock<INetwork>().Object,
            new Mock<IIssueOwnerResolver>().Object,
            new Mock<IIssueOwnershipRegistry>().Object,
            new Mock<IPlayerManager>().Object);
        var captured = strategy.Capture(issue);
        Assert.Equal(sizeof(long), captured.Length);
        var party = ObjectHelper.SkipConstructor<TaleWorlds.CampaignSystem.Party.MobileParty>();
        quest._lesserNobleParty = party;
        quest._firstTalkIsDone = true;
        quest._giveNotificationAfterHoursCounter = 9;
        var villages = quest._suitableVillagesToVisitList;

        quest.QuestDueTime = CampaignTime.Never;
        strategy.Apply(issue, captured);

        Assert.Equal(CampaignTime.Days(12f), quest.QuestDueTime);
        Assert.Same(party, quest._lesserNobleParty);
        Assert.Same(villages, quest._suitableVillagesToVisitList);
        Assert.True(quest._firstTalkIsDone);
        Assert.Equal(9, quest._giveNotificationAfterHoursCounter);
    }

    [Fact]
    public void AcceptCapture_TruncatedDueTimeLeavesTheQuestUnchanged()
    {
        var issue = ObjectHelper.SkipConstructor<Issue>();
        var quest = ObjectHelper.SkipConstructor<Quest>();
        issue.IssueQuest = quest;
        quest.QuestDueTime = CampaignTime.Days(12f);
        var strategy = new LesserNobleRevoltQuestSolutionAcceptStrategy(
            new Mock<IObjectManager>().Object,
            new Mock<INetwork>().Object,
            new Mock<IIssueOwnerResolver>().Object,
            new Mock<IIssueOwnershipRegistry>().Object,
            new Mock<IPlayerManager>().Object);

        Assert.Throws<EndOfStreamException>(() => strategy.Apply(issue, new byte[7]));

        Assert.Equal(CampaignTime.Days(12f), quest.QuestDueTime);
    }

    [Fact]
    public void BranchProofValues_AreStableForEveryVanillaEnding()
    {
        Assert.Equal(1, (byte)LesserNobleRevoltBranch.SuccessByAgreement);
        Assert.Equal(2, (byte)LesserNobleRevoltBranch.SuccessByBattle);
        Assert.Equal(3, (byte)LesserNobleRevoltBranch.FailureByBattle);
        Assert.Equal(4, (byte)LesserNobleRevoltBranch.FailureByRelease);
    }

    [Theory]
    [InlineData(true, false, true, true, (int)RevoltBattleEnd.OwnerLost)]
    [InlineData(true, false, true, false, (int)RevoltBattleEnd.OwnerWon)]
    [InlineData(true, true, true, true, (int)RevoltBattleEnd.OwnerLost)]
    [InlineData(true, true, true, false, (int)RevoltBattleEnd.OwnerWon)]
    [InlineData(true, false, false, false, (int)RevoltBattleEnd.NoWinner)]
    [InlineData(true, true, false, false, (int)RevoltBattleEnd.NoWinner)]
    [InlineData(false, true, true, true, (int)RevoltBattleEnd.OwnerLost)]
    [InlineData(false, true, true, false, (int)RevoltBattleEnd.OwnerWon)]
    [InlineData(false, true, false, false, (int)RevoltBattleEnd.Ignored)]
    [InlineData(false, false, true, true, (int)RevoltBattleEnd.Ignored)]
    [InlineData(false, false, false, false, (int)RevoltBattleEnd.Ignored)]
    [InlineData(false, false, true, false, (int)RevoltBattleEnd.ThirdPartyWon)]
    public void BattleEnd_UsesOwnerParticipationAndTheRecordedRetreat(bool ownerStarted, bool ownerInvolved, bool hasWinner, bool revoltWon, int expected)
    {
        Assert.Equal((RevoltBattleEnd)expected, LesserNobleRevoltWorldEventService.ClassifyBattleEnd(ownerStarted, ownerInvolved, hasWinner, revoltWon));
    }

    [Fact]
    public void BattleState_IsSharedAcrossMissionScopesAndEmptyInTheNextSession()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        var owner = ObjectHelper.SkipConstructor<Hero>();
        using (var session = CreateSession())
        {
            var service = (LesserNobleRevoltWorldEventService)session.Resolve<ILesserNobleRevoltWorldEventService>();
            service.RecordOwnerBattle(quest, owner, null);
            using (var mission = session.BeginLifetimeScope())
            {
                Assert.Same(service, mission.Resolve<ILesserNobleRevoltWorldEventService>());
                Assert.True(mission.Resolve<ILesserNobleRevoltWorldEventService>().IsProtectedHero(owner));
            }
            Assert.True(session.Resolve<ILesserNobleRevoltWorldEventService>().IsProtectedHero(owner));
        }

        using var nextSession = CreateSession();
        Assert.False(nextSession.Resolve<ILesserNobleRevoltWorldEventService>().IsProtectedHero(owner));

        static IContainer CreateSession()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<IssuesModule>();
            builder.RegisterInstance(new Mock<IObjectManager>().Object);
            builder.RegisterInstance(new Mock<INetwork>().Object);
            builder.RegisterInstance(new Mock<IIssueOwnerResolver>().Object);
            builder.RegisterInstance(new Mock<IPlayerManager>().Object);
            builder.RegisterInstance(new Mock<IOutcomeDispatcher>().Object);
            return builder.Build();
        }
    }

    [Fact]
    public void Notification_IsAppliedOncePerVisit()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest._visitedVillagesList = new List<Settlement> { ObjectHelper.SkipConstructor<Settlement>() };
        quest._giveNotificationAfterHoursCounter = 9;

        Assert.True(LesserNobleRevoltWorldEventService.ShouldApplyClientNotification(quest, 1));
        Assert.False(LesserNobleRevoltWorldEventService.ShouldApplyClientNotification(quest, 2));

        quest._giveNotificationAfterHoursCounter = -1;

        Assert.False(LesserNobleRevoltWorldEventService.ShouldApplyClientNotification(quest, 1));
    }

    [Fact]
    public void SettlementProgress_IgnoresAVisitTheClientAlreadyHolds()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        var village = ObjectHelper.SkipConstructor<Settlement>();
        quest._visitedVillagesList = new List<Settlement>();
        quest._suitableVillagesToVisitList = new List<Settlement> { village };

        Assert.True(LesserNobleRevoltWorldEventService.ApplyClientSettlementProgress(quest, village, 1));
        Assert.False(LesserNobleRevoltWorldEventService.ApplyClientSettlementProgress(quest, village, 1));
        Assert.True(LesserNobleRevoltWorldEventService.ApplyClientSettlementProgress(quest, village, 2));

        Assert.Equal(2, quest._visitedVillagesList.Count);
        Assert.Empty(quest._suitableVillagesToVisitList);
    }
}
