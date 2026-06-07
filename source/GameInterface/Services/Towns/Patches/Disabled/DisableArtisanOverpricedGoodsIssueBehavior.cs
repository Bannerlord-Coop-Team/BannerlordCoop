using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(ArtisanOverpricedGoodsIssueBehavior))]
internal class DisableArtisanOverpricedGoodsIssueBehavior
{
    [HarmonyPatch(nameof(ArtisanOverpricedGoodsIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
