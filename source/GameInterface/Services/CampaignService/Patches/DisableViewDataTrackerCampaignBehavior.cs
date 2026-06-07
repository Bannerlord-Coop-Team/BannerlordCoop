using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(ViewDataTrackerCampaignBehavior))]
internal class DisableViewDataTrackerCampaignBehavior
{
    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsClient;
    }
}
