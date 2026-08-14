using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.ArtisanOverpricedGoods;

using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

[QuestTypeModule]
internal static class ArtisanOverpricedGoodsQuestType
{
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.ItemRoster.GetItemNumber(quest._requestedTradeGood) + quest._givenTradeGoods >= quest._requestedTradeGoodAmount;
    }

    static ArtisanOverpricedGoodsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("ArtisanOverpricedGoods")
            .WithQuestSolutionAccept()
            .WithAlternativeAccept()
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
