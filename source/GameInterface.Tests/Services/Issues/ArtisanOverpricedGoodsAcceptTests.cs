using GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;
using HarmonyLib;
using System.Runtime.Serialization;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

namespace GameInterface.Tests.Services.Issues;

public class ArtisanOverpricedGoodsAcceptTests
{
    [Theory]
    [InlineData(1, 50)]
    [InlineData(35, 2400)]
    public void AcceptCapture_PreservesTheQuotedDeliveryAmountAndPayment(int amount, int payment)
    {
        var strategy = new ArtisanOverpricedGoodsQuestSolutionAcceptStrategy();
        var source = NewIssue(amount, payment);
        var mirror = NewIssue(99, 1);

        strategy.Apply(mirror, strategy.Capture(source));

        var quest = (Quest)mirror.IssueQuest;
        Assert.Equal(amount, quest._requestedTradeGoodAmount);
        Assert.Equal(payment, quest._rewardGold);
    }

    [Fact]
    public void AcceptCapture_LeavesOtherPlayersWithoutAQuestObject()
    {
        var strategy = new ArtisanOverpricedGoodsQuestSolutionAcceptStrategy();
        var observer = (Issue)FormatterServices.GetUninitializedObject(typeof(Issue));

        strategy.Apply(observer, strategy.Capture(NewIssue(5, 900)));

        Assert.Null(observer.IssueQuest);
    }

    private static Issue NewIssue(int amount, int payment)
    {
        var quest = (Quest)FormatterServices.GetUninitializedObject(typeof(Quest));
        AccessTools.Field(typeof(Quest), nameof(Quest._requestedTradeGoodAmount)).SetValue(quest, amount);
        quest._rewardGold = payment;
        var issue = (Issue)FormatterServices.GetUninitializedObject(typeof(Issue));
        issue.IssueQuest = quest;
        return issue;
    }
}
