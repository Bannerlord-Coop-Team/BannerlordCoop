using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Clans.Patches.Disable;

[HarmonyPatch(typeof(PeaceOfferCampaignBehavior))]
internal class DisablePeaceOfferCampaignBehavior
{
    [HarmonyPatch(nameof(PeaceOfferCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
