using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(TeleportationCampaignBehavior))]
internal class DisableTeleportationCampaignBehavior
{
    [HarmonyPatch(nameof(TeleportationCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
