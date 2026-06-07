using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(DynamicBodyCampaignBehavior))]
internal class DisableDynamicBodyCampaignBehavior
{
    [HarmonyPatch(nameof(DynamicBodyCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
