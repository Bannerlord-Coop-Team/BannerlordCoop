using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior))]
internal class DisableArtisanCantSellProductsAtAFairPriceIssueBehavior
{
    [HarmonyPatch(nameof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
