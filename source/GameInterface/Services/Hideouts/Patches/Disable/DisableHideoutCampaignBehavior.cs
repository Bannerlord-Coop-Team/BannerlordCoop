using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Hideouts.Patches.Disable;

[HarmonyPatch(typeof(HideoutCampaignBehavior))]
internal class DisableHideoutCampaignBehavior
{
    [HarmonyPatch(nameof(HideoutCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
