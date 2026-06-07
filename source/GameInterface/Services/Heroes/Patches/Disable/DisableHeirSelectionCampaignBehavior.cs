using HarmonyLib;
using SandBox.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(HeirSelectionCampaignBehavior))]
internal class DisableHeirSelectionCampaignBehavior
{
    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
