using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Clans.Patches.Disable;

[HarmonyPatch(typeof(CrimeCampaignBehavior))]
internal class DisableCrimeCampaignBehavior
{
    [HarmonyPatch(nameof(CrimeCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
