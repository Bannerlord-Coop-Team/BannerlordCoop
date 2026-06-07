using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.SiegeEvents.Patches.Disable;

[HarmonyPatch(typeof(SiegeAmbushCampaignBehavior))]
internal class DisableSiegeAmbushCampaignBehavior
{
    [HarmonyPatch(nameof(SiegeAmbushCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
