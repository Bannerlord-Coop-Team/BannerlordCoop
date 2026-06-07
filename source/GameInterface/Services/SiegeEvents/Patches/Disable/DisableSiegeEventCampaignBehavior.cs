using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.SiegeEvents.Patches.Disable;

[HarmonyPatch(typeof(SiegeEventCampaignBehavior))]
internal class DisableSiegeEventCampaignBehavior
{
    [HarmonyPatch(nameof(SiegeEventCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
