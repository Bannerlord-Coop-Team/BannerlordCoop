using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LordsNeedsTutor;
using Moq;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;

namespace GameInterface.Tests.Services.Issues;

public sealed class LordsNeedsTutorAcceptTests
{
    private readonly LordsNeedsTutorQuestSolutionAcceptStrategy strategy = new(Mock.Of<IIssueOwnerResolver>());

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CapturePreservesTheChosenGiftCount(int reward)
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest._randomForQuestReward = reward;
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue.IssueQuest = quest;
        Assert.Equal(reward, BitConverter.ToInt32(strategy.Capture(issue), 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void InvalidGiftCountIsRefusedBeforeAnyQuestAction(int reward)
    {
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue.IssueQuest = ObjectHelper.SkipConstructor<Quest>();
        Assert.Throws<ArgumentException>(() => strategy.Apply(issue, BitConverter.GetBytes(reward)));
    }

    [Fact]
    public void MissingGiftCaptureIsRefused()
    {
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue.IssueQuest = ObjectHelper.SkipConstructor<Quest>();
        Assert.Throws<ArgumentException>(() => strategy.Apply(issue, Array.Empty<byte>()));
    }
}
