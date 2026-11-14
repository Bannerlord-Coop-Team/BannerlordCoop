using GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Moq;
using ProtoBuf;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

namespace GameInterface.Tests.Services.Issues;

public class HeadmanNeedsGrainAcceptTests
{
    [Fact]
    public void AcceptCapture_PreservesTheAcceptedAmountRewardAndDeadline()
    {
        var source = IssueWithQuest(68, 0, new CampaignTime(123456));
        var mirror = IssueWithQuest(12, 50, new CampaignTime(1));
        var strategy = new HeadmanNeedsGrainQuestSolutionAcceptStrategy();

        strategy.Apply(mirror, strategy.Capture(source));

        var quest = (Quest)mirror.IssueQuest;
        Assert.Equal(68, quest._neededGrainAmount);
        Assert.Equal(0, quest._rewardGold);
        Assert.Equal(123456, quest.QuestDueTime.NumTicks);
    }

    [Fact]
    public void AcceptCapture_LeavesAClientWithoutAQuestObjectAlone()
    {
        var issue = (Issue)FormatterServices.GetUninitializedObject(typeof(Issue));
        var strategy = new HeadmanNeedsGrainQuestSolutionAcceptStrategy();
        Assert.Empty(strategy.Capture(issue));
        strategy.Apply(issue, BitConverter.GetBytes(68));
        Assert.Null(issue.IssueQuest);
    }

    [Fact]
    public void AcceptCapture_TruncatedPayloadDoesNotThrow()
    {
        var issue = IssueWithQuest(12, 50, new CampaignTime(123456));
        var strategy = new HeadmanNeedsGrainQuestSolutionAcceptStrategy();

        strategy.Apply(issue, new byte[] { 1 });
    }
    [Fact]
    public void CreationCapture_EmptyPayloadLeavesTheIssueUnchanged()
    {
        var issue = IssueWithQuest(12, 0, new CampaignTime(1));
        var strategy = new HeadmanNeedsGrainCreationCaptureStrategy(null);

        strategy.ApplyAfterCreation(issue, Array.Empty<byte>());

        var grainIssue = (Issue)issue;
        Assert.Equal(0f, grainIssue._issueDifficultyMultiplier);
        Assert.Equal(0, grainIssue.IssueDueTime.NumTicks);
    }

    [Fact]
    public void CreationCapture_TruncatedPayloadDoesNotThrow()
    {
        var issue = IssueWithQuest(12, 0, new CampaignTime(1));
        var strategy = new HeadmanNeedsGrainCreationCaptureStrategy(null);

        strategy.ApplyAfterCreation(issue, new byte[] { 1 });
    }

    [Fact]
    public void CreationCapture_RoundTripsDifficultyAndDeadline()
    {
        var source = IssueWithQuest(12, 0, new CampaignTime(1));
        var sourceIssue = (Issue)source;
        sourceIssue._issueDifficultyMultiplier = 0.25f;
        sourceIssue.IssueDueTime = new CampaignTime(123456);
        var market = (Settlement)RuntimeHelpers.GetUninitializedObject(typeof(Settlement));
        sourceIssue._nearbySuitableSettlementCache = market;
        var target = IssueWithQuest(12, 0, new CampaignTime(1));
        var objects = new Mock<IObjectManager>();
        var marketId = "market";
        objects.Setup(manager => manager.TryGetIdWithLogging(market, out marketId)).Returns(true);
        var strategy = new HeadmanNeedsGrainCreationCaptureStrategy(objects.Object);

        strategy.ApplyAfterCreation(target, strategy.Capture(source));

        var targetIssue = (Issue)target;
        Assert.Equal(0.25f, targetIssue._issueDifficultyMultiplier);
        Assert.Equal(123456, targetIssue.IssueDueTime.NumTicks);
    }

    [Fact]
    public void PriceSnapshot_PreservesTheCompanionFundingPriceOnTheWire()
    {
        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, new NetworkHeadmanGrainPriceUpdated(31));
            stream.Position = 0;
            Assert.Equal(31, Serializer.Deserialize<NetworkHeadmanGrainPriceUpdated>(stream).Price);
        }
    }

    private static Issue IssueWithQuest(int needed, int reward, CampaignTime deadline)
    {
        var quest = (Quest)FormatterServices.GetUninitializedObject(typeof(Quest));
        AccessTools.Field(typeof(Quest), nameof(Quest._neededGrainAmount)).SetValue(quest, needed);
        quest._rewardGold = reward;
        quest.QuestDueTime = deadline;
        var issue = (Issue)FormatterServices.GetUninitializedObject(typeof(Issue));
        issue.IssueQuest = quest;
        return issue;
    }
}
