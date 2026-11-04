using Common.Logging;
using Serilog;
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
    private static readonly ILogger Logger = LogManager.GetLogger<ArtisanProductsFinalizationProofStrategy>();
    private readonly IArtisanProductsProgress progress;

    public ArtisanProductsFinalizationProofStrategy(IArtisanProductsProgress progress)
    {
        this.progress = progress;
    }

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest artisan)) return false;
        var party = Hero.MainHero?.PartyBelongedTo;
        if (party == null || !artisan.IsOngoing) { Logger.Warning("Skipping Artisan Products branch {proof}: owner party or ongoing quest unavailable", proof); return true; }
        var branch = (ArtisanProductsBranch)proof;
        var delivery = branch >= ArtisanProductsBranch.PartialDelivery && branch <= ArtisanProductsBranch.RefuseDelivery;
        if (delivery && party.CurrentSettlement != artisan._targetSettlement) { Logger.Warning("Skipping Artisan Products branch {proof}: owner is not at the target settlement", proof); return true; }
        var carried = party.ItemRoster.GetItemNumber(artisan._rawMaterialsToBeDelivered);
        var remaining = artisan._amountOfRawGoodsToBeDelivered - artisan._deliveredRawGoods;

        switch (branch)
        {
            case ArtisanProductsBranch.PartialDelivery:
                if (carried > 0 && carried < remaining) artisan.DeliverItemsPartiallyOnConsequence();
                else Logger.Warning("Skipping Artisan Products partial branch: carried {carried}, remaining {remaining}", carried, remaining);
                break;
            case ArtisanProductsBranch.FullDelivery:
                if (remaining > 0 && carried >= remaining) artisan.DeliverItemsFullyOnConsequence();
                else Logger.Warning("Skipping Artisan Products full branch: carried {carried}, remaining {remaining}", carried, remaining);
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
