using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Framework.Dismissal;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace GameInterface.Tests.Services.Issues;

/// <summary>Checks the order of the ending steps through the vanilla adapter, without Harmony.</summary>
public class IssueEndingCoordinatorTests
{
    private const string GiverId = "giver";

    private readonly List<string> steps = new();
    private readonly Mock<IIssueDismisser> issueDismisser = new();
    private readonly IssueEndingCoordinator coordinator;
    private readonly Issue issue = ObjectHelper.SkipConstructor<Issue>();

    public IssueEndingCoordinatorTests()
    {
        issue.StringId = "issue_1";
        issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;

        var giverId = GiverId;
        var objects = new Mock<IObjectManager>();
        objects.Setup(value => value.TryGetIdWithLogging(It.IsAny<Hero>(), out giverId)).Returns(true);
        var ownership = new Mock<IIssueOwnershipRegistry>();
        ownership.Setup(value => value.Remove(GiverId, issue.StringId)).Callback(() => steps.Add("drop owner"));
        var broker = new Mock<IMessageBroker>();
        broker.Setup(value => value.Publish(It.IsAny<object>(), It.IsAny<IssueOutcomeObserved>()))
            .Callback(() => steps.Add("announce"));
        var claims = new Mock<IIssueEndingClaimRegistry>();
        claims.Setup(value => value.Get(issue)).Returns(new IssueEndingClaim());

        issueDismisser.Setup(value => value.ReturnTroops(issue)).Callback(() => steps.Add("return troops"));
        issueDismisser.Setup(value => value.AddTroopsReturnedLog(issue)).Callback(() => steps.Add("troops returned log"));
        issueDismisser.Setup(value => value.SendCancel(issue)).Callback(() => steps.Add("send cancel"));
        issueDismisser.Setup(value => value.CloseJournal(issue, IssueBase.IssueUpdateDetails.IssueCancel))
            .Callback(() => steps.Add("close journal"));
        issueDismisser.Setup(value => value.ForceRemove(issue)).Callback(() => steps.Add("force remove"));
        issueDismisser.Setup(value => value.DropUnfinishedJournal(issue)).Callback(() => steps.Add("drop journal"));

        coordinator = new IssueEndingCoordinator(broker.Object, ownership.Object, objects.Object, issueDismisser.Object,
            Mock.Of<IIssueOwnerResolver>(), claims.Object);
    }

    [Fact]
    public void TroopEndingAsOwnerReturnsTheTroopsFirstAndAnnouncesOnceTheIssueLeftTheManager()
    {
        var owner = new Mock<IDisposable>();
        owner.Setup(value => value.Dispose()).Callback(() => steps.Add("dispose owner"));
        issueDismisser.Setup(value => value.IsInManager(issue)).Returns(false);

        coordinator.EndAsOwner(issue, IssueOutcome.QuestCancel, () => steps.Add("ending"), owner.Object);

        Assert.Equal(new[]
        {
            "return troops", "troops returned log", "send cancel", "ending", "drop owner", "close journal", "announce",
            "dispose owner",
        }, steps);
    }

    [Fact]
    public void EndingThatLeavesTheIssueInTheManagerIsAnnouncedOnlyByTheDailyRetry()
    {
        issueDismisser.SetupSequence(value => value.IsInManager(issue)).Returns(true).Returns(false);

        coordinator.EndAsOwner(issue, IssueOutcome.QuestCancel, () => steps.Add("ending"), Mock.Of<IDisposable>());
        Assert.DoesNotContain("announce", steps);

        steps.Clear();
        coordinator.RetryFailedEndings();
        coordinator.RetryFailedEndings();

        // The retry takes it out without running the ending again, and only the first retry finds it
        Assert.Equal(new[] { "force remove", "drop owner", "drop journal", "announce" }, steps);
    }
}
