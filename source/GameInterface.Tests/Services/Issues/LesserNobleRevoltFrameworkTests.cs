using Common.Util;
using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt;
using Moq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Xunit;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;
using RevoltBattleEnd = GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches.LesserNobleRevoltWorldEventPatches.RevoltBattleEnd;

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
            new Mock<IIssueOwnershipRegistry>().Object);
        var captured = strategy.Capture(issue);

        quest.QuestDueTime = CampaignTime.Never;
        strategy.Apply(issue, captured);

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

    [Fact]
    public void BattleEnd_RevoltVictoryAfterTheOwnerRetreatedFailsTheQuest()
    {
        // The retreating owner is no longer in InvolvedParties, only in the recorded battle.
        Assert.Equal(RevoltBattleEnd.OwnerLost,
            LesserNobleRevoltWorldEventPatches.ClassifyBattleEnd(ownerStarted: true, ownerInvolved: false, hasWinner: true, revoltWon: true));
        Assert.Equal(RevoltBattleEnd.OwnerWon,
            LesserNobleRevoltWorldEventPatches.ClassifyBattleEnd(ownerStarted: true, ownerInvolved: true, hasWinner: true, revoltWon: false));
        Assert.Equal(RevoltBattleEnd.NoWinner,
            LesserNobleRevoltWorldEventPatches.ClassifyBattleEnd(ownerStarted: true, ownerInvolved: false, hasWinner: false, revoltWon: false));
    }

    [Fact]
    public void BattleEnd_RevoltVictoryOverAThirdPartyLeavesTheQuestAlone()
    {
        Assert.Equal(RevoltBattleEnd.Ignored,
            LesserNobleRevoltWorldEventPatches.ClassifyBattleEnd(ownerStarted: false, ownerInvolved: false, hasWinner: true, revoltWon: true));
        Assert.Equal(RevoltBattleEnd.Ignored,
            LesserNobleRevoltWorldEventPatches.ClassifyBattleEnd(ownerStarted: false, ownerInvolved: false, hasWinner: false, revoltWon: false));
        Assert.Equal(RevoltBattleEnd.ThirdPartyWon,
            LesserNobleRevoltWorldEventPatches.ClassifyBattleEnd(ownerStarted: false, ownerInvolved: false, hasWinner: true, revoltWon: false));
    }

    [Fact]
    public void Notification_IsAppliedOncePerVisit()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest._visitedVillagesList = new List<Settlement> { ObjectHelper.SkipConstructor<Settlement>() };
        quest._giveNotificationAfterHoursCounter = 9;

        Assert.True(LesserNobleRevoltWorldEventPatches.ShouldApplyClientNotification(quest, 1));
        Assert.False(LesserNobleRevoltWorldEventPatches.ShouldApplyClientNotification(quest, 2));

        quest._giveNotificationAfterHoursCounter = -1;

        Assert.False(LesserNobleRevoltWorldEventPatches.ShouldApplyClientNotification(quest, 1));
    }

    [Fact]
    public void SettlementProgress_IgnoresAVisitTheClientAlreadyHolds()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        var village = ObjectHelper.SkipConstructor<Settlement>();
        quest._visitedVillagesList = new List<Settlement>();
        quest._suitableVillagesToVisitList = new List<Settlement> { village };

        Assert.True(LesserNobleRevoltWorldEventPatches.ApplyClientSettlementProgress(quest, village, 1));
        Assert.False(LesserNobleRevoltWorldEventPatches.ApplyClientSettlementProgress(quest, village, 1));
        Assert.True(LesserNobleRevoltWorldEventPatches.ApplyClientSettlementProgress(quest, village, 2));

        Assert.Equal(2, quest._visitedVillagesList.Count);
        Assert.Empty(quest._suitableVillagesToVisitList);
    }
}
