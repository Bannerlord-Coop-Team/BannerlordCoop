using Common;
using Common.Util;
using GameInterface.Services.Issues.Quests.ArtisanProducts;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

namespace GameInterface.Tests.Services.Issues;

[Collection(ModInformationRoleCollection.Name)]
public class ArtisanProductsCaptureTests
{
    [Fact]
    public void AcceptCapture_PreservesTheQuotedAmountRewardAndDeadline()
    {
        var strategy = new ArtisanProductsQuestSolutionAcceptStrategy();
        var source = ObjectHelper.SkipConstructor<Issue>();
        var quest = ObjectHelper.SkipConstructor<Quest>();
        AccessTools.Field(typeof(Quest), nameof(Quest._amountOfRawGoodsToBeDelivered)).SetValue(quest, 73);
        quest._rewardGold = 2400;
        quest.QuestDueTime = new CampaignTime(987654321);
        source.IssueQuest = quest;
        var target = ObjectHelper.SkipConstructor<Issue>();
        target.IssueQuest = ObjectHelper.SkipConstructor<Quest>();
        var wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = false;
            strategy.Apply(target, strategy.Capture(source));
            var mirror = (Quest)target.IssueQuest;
            Assert.Equal(73, mirror._amountOfRawGoodsToBeDelivered);
            Assert.Equal(2400, mirror.RewardGold);
            Assert.Equal(2400, mirror._rewardGold);
            Assert.Equal(987654321, mirror.QuestDueTime.NumTicks);
        }
        finally { ModInformation.IsServer = wasServer; }
    }

    [Fact]
    public void AcceptCapture_WithNoQuestIsEmpty()
    {
        Assert.Empty(new ArtisanProductsQuestSolutionAcceptStrategy().Capture(ObjectHelper.SkipConstructor<Issue>()));
    }

    [Fact]
    public void TruncatedAcceptCapture_ThrowsBeforeGrantingAnyItems()
    {
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue.IssueQuest = ObjectHelper.SkipConstructor<Quest>();
        Assert.Throws<System.IO.EndOfStreamException>(() => new ArtisanProductsQuestSolutionAcceptStrategy().Apply(issue, Array.Empty<byte>()));
    }
}
