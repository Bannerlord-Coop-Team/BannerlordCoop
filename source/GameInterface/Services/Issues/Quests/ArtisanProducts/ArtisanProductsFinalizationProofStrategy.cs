using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts;

internal enum ArtisanProductsBranch : byte
{
    PartialDelivery = 1,
    FullDelivery,
    RefuseDelivery,
    SurrenderGoods,
    RefuseMerchant,
}

internal class ArtisanProductsFinalizationProofStrategy : IFinalizationProofStrategy
{
    private readonly IArtisanProductsProgress progress;

    public ArtisanProductsFinalizationProofStrategy(IArtisanProductsProgress progress)
    {
        this.progress = progress;
    }

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest artisan)) return false;
        var party = Hero.MainHero?.PartyBelongedTo;
        if (party == null || !artisan.IsOngoing) return true;
        var branch = (ArtisanProductsBranch)proof;
        var delivery = branch >= ArtisanProductsBranch.PartialDelivery && branch <= ArtisanProductsBranch.RefuseDelivery;
        var origin = TaleWorlds.CampaignSystem.Issues.IssueManager.GetIssueOfQuest(quest)?.IssueOwner.CurrentSettlement;
        if (party.CurrentSettlement != (delivery ? artisan._targetSettlement : origin)) return true;
        var carried = party.ItemRoster.GetItemNumber(artisan._rawMaterialsToBeDelivered);
        var remaining = artisan._amountOfRawGoodsToBeDelivered - artisan._deliveredRawGoods;

        switch (branch)
        {
            case ArtisanProductsBranch.PartialDelivery:
                if (carried > 0 && carried < remaining) artisan.DeliverItemsPartiallyOnConsequence();
                break;
            case ArtisanProductsBranch.FullDelivery:
                if (remaining > 0 && carried >= remaining) artisan.DeliverItemsFullyOnConsequence();
                break;
            case ArtisanProductsBranch.RefuseDelivery:
                artisan.DeliverItemsRejectOnConsequence();
                break;
            case ArtisanProductsBranch.SurrenderGoods:
                if (!artisan._counterOfferRefused) artisan.QuestFailedWithRefusal();
                break;
            case ArtisanProductsBranch.RefuseMerchant:
                if (!artisan._counterOfferRefused) artisan.RefuseCounterOfferConsequences();
                break;
            default:
                return false;
        }
        if (artisan.IsOngoing) progress.Send(artisan);
        return true;
    }
}
