using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.SiegeEvents.Patches.Disable;

[HarmonyPatch(typeof(SiegeAftermathCampaignBehavior))]
internal class DisableSiegeAftermathCampaignBehavior
{
    [HarmonyPatch(nameof(SiegeAftermathCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
