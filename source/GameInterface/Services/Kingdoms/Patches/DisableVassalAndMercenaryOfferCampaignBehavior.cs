using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(VassalAndMercenaryOfferCampaignBehavior))]
internal class DisableVassalAndMercenaryOfferCampaignBehavior
{
    [HarmonyPatch(nameof(VassalAndMercenaryOfferCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
