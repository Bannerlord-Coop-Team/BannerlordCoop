using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using System;
using GameInterface.Services.Issues.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Generic;

internal sealed class ArtisanOverpricedGoodsActionGuard : IDisposable
{
    [ThreadStatic]
    private static Hero activeGiver;
    [ThreadStatic]
    private static ArtisanOverpricedGoodsAction activeAction;

    private readonly Hero previousGiver;
    private readonly ArtisanOverpricedGoodsAction previousAction;
    private readonly PropertyOwner<PropertyObject> previousTraits;
    private readonly Campaign campaign;

    public ArtisanOverpricedGoodsActionGuard(Hero giver, Hero player, ArtisanOverpricedGoodsAction action)
    {
        previousGiver = activeGiver;
        previousAction = activeAction;
        campaign = Campaign.Current;
        previousTraits = campaign.PlayerTraitDeveloper;
        var registry = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress;
        if (!registry.TryGet(player, out var progress))
        {
            progress = new PropertyOwner<PropertyObject>();
            progress.SetPropertyValue(DefaultTraits.Honor,
                campaign.Models.CharacterDevelopmentModel.GetTraitXpRequiredForTraitLevel(
                    DefaultTraits.Honor, player.GetTraitLevel(DefaultTraits.Honor)));
            registry.Set(player, progress);
        }
        // Share the persisted owner XP so different quest types advance the same player's traits.
        campaign.PlayerTraitDeveloper = progress;
        activeGiver = giver;
        activeAction = action;
    }

    public static bool IsActiveFor(Hero giver) => giver != null && activeGiver == giver;
    public static bool IsRefusingLordOffer(Hero giver) => IsActiveFor(giver) && activeAction == ArtisanOverpricedGoodsAction.RefuseLordOffer;

    public void Dispose()
    {
        campaign.PlayerTraitDeveloper = previousTraits;
        activeGiver = previousGiver;
        activeAction = previousAction;
    }
}
