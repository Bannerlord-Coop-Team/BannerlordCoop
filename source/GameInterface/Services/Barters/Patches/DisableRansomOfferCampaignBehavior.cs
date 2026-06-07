using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(RansomOfferCampaignBehavior))]
internal class DisableRansomOfferCampaignBehavior
{
    [HarmonyPatch(nameof(RansomOfferCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
