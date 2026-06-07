using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(MarriageOfferCampaignBehavior))]
internal class DisableMarriageOfferCampaignBehavior
{
    [HarmonyPatch(nameof(MarriageOfferCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
