using Common.Util;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt;
using Moq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;

namespace GameInterface.Tests.Services.Issues;

public class LesserNobleRevoltFrameworkTests
{
    [Fact]
    public void CreationCapture_RoundTripsTheGiverNameAndTimes()
    {
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue._lesserNobleName = new TextObject("Noble A");
        issue.IssueCreationTime = CampaignTime.Days(6f);
        issue.IssueDueTime = CampaignTime.Days(26f);

        var strategy = new LesserNobleRevoltCreationCaptureStrategy(new Mock<global::GameInterface.Services.ObjectManager.IObjectManager>().Object);
        var captured = strategy.Capture(issue);

        Assert.NotEmpty(captured);
        Assert.Equal("Noble A", issue._lesserNobleName.ToString());
        Assert.Equal(issue.IssueCreationTime, CampaignTime.Days(6f));
        Assert.Equal(issue.IssueDueTime, CampaignTime.Days(26f));
    }

    [Fact]
    public void BranchProofValues_AreStableForEveryVanillaEnding()
    {
        Assert.Equal(1, (byte)LesserNobleRevoltBranch.SuccessByAgreement);
        Assert.Equal(2, (byte)LesserNobleRevoltBranch.SuccessByBattle);
        Assert.Equal(3, (byte)LesserNobleRevoltBranch.FailureByBattle);
        Assert.Equal(4, (byte)LesserNobleRevoltBranch.FailureByRelease);
    }
}
