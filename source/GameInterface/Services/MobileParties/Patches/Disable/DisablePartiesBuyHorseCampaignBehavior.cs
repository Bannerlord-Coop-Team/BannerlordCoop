using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(PartiesBuyHorseCampaignBehavior))]
internal class DisablePartiesBuyHorseCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesBuyHorseCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
