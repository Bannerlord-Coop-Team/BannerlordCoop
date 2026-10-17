using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtOfTrade;

internal class ArtOfTradeFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest trade) || !trade.IsOngoing) return false;

        bool commissioned = trade._soldItemAmountLog != null;
        switch ((ArtOfTradeBranch)proof)
        {
            case ArtOfTradeBranch.SellOnCommission:
                if (!commissioned) trade.QuestAcceptedConsequences();
                return true;
            case ArtOfTradeBranch.BuyGoods:
                if (!commissioned && trade.PlayerBuyClickableOptionCondition(out _)) trade.QuestFinishedPlayerBoughtTheGoods();
                return true;
            case ArtOfTradeBranch.PaySaleProceeds:
                if (commissioned && !trade._productsUndersold && trade.PlayerPayAgreedDenarsClickableCondition(out _))
                    trade.QuestSuccessPlayerSoldTheProducts();
                return true;
            case ArtOfTradeBranch.PayAgreedSum:
                if (CanSettle(trade) && trade._productsUndersold && trade.PlayerPayAgreedDenarsClickableCondition(out _))
                    trade.PlayerPaidAgreedDenarsQuestSuccess();
                return true;
            case ArtOfTradeBranch.BreakAgreement:
                if (CanSettle(trade) && trade._productsUndersold && trade.PlayerFailWithGoldClickableCondition(out _))
                    trade.QuestFailedPlayerBrokeTheAgreement();
                return true;
            default:
                return false;
        }
    }

    private static bool CanSettle(Quest quest)
    {
        // The dedicated server has no conversation hero, keep vanilla's remaining progress checks.
        return quest._soldItemAmountLog != null &&
            (quest.ItemCountLeftToSell <= 0 || quest._gatheredDenars >= quest._targetDenarsToAchieve ||
             (quest._daysPassed >= 1 && quest.CheckIfPlayerLostItem()));
    }
}
