using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(ViewDataTrackerCampaignBehavior))]
internal class DisableViewDataTrackerCampaignBehavior
{
    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
