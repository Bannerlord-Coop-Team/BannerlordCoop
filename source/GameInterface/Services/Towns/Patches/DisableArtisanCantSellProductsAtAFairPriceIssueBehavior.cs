using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior))]
internal class DisableArtisanCantSellProductsAtAFairPriceIssueBehavior
{
    [HarmonyPatch(nameof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
