using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Banners.Patches;

[HarmonyPatch(typeof(BannerCampaignBehavior))]
internal class DisableBannerCampaignBehavior
{
    [HarmonyPatch(nameof(BannerCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
