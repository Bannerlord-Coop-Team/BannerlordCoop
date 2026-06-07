using HarmonyLib;
using SandBox.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(GuardsCampaignBehavior))]
internal class DisableGuardsCampaignBehavior
{
    [HarmonyPatch(nameof(GuardsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
