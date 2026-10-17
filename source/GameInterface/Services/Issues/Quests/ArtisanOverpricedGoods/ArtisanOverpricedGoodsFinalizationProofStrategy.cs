using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;

internal class ArtisanOverpricedGoodsFinalizationProofStrategy : IFinalizationProofStrategy
{
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ArtisanOverpricedGoodsFinalizationProofStrategy(INetwork network, IObjectManager objectManager)
    {
        this.network = network;
        this.objectManager = objectManager;
    }

    public bool TryRunBranch(QuestBase questBase, byte proof)
    {
        if (!(questBase is Quest quest)) return false;
        var branch = (ArtisanOverpricedGoodsBranch)proof;
        if (branch < ArtisanOverpricedGoodsBranch.PartialDelivery || branch > ArtisanOverpricedGoodsBranch.AcceptMerchantOffer) return false;

        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || MobileParty.MainParty.CurrentSettlement != quest.QuestGiver.CurrentSettlement) return true;

        if (branch == ArtisanOverpricedGoodsBranch.AcceptMerchantOffer)
        {
            if (quest.AntagonistHero?.IsActive == true) quest.AcceptCounterOffer();
            return true;
        }

        var remaining = quest._requestedTradeGoodAmount - quest._givenTradeGoods;
        var carried = PartyBase.MainParty.ItemRoster.GetItemNumber(quest._requestedTradeGood);
        if (remaining <= 0 || carried <= 0) return true;

        // The server's quest never starts, but vanilla's delivery methods still need a journal stage.
        if (quest._playerStartsQuestLog == null)
        {
            quest._playerStartsQuestLog = new JournalLog(CampaignTime.Now, TextObject.GetEmpty(), null,
                quest._givenTradeGoods, quest._requestedTradeGoodAmount);
        }

        if (branch == ArtisanOverpricedGoodsBranch.PartialDelivery)
        {
            if (carried >= remaining) return true;
            quest.DeliverItemsPartiallyOnConsequence();
        }
        else
        {
            if (carried < remaining) return true;
            quest.DeliverItemsFullyOnConsequence();
        }

        if (objectManager.TryGetIdWithLogging(quest.QuestGiver, out var giverId))
        {
            network.SendAll(new NetworkArtisanDeliveryProgress(giverId, issue.StringId, quest._givenTradeGoods));
        }

        if (branch == ArtisanOverpricedGoodsBranch.FullDelivery) quest.CompleteQuestWithSuccess();
        return true;
    }
}
