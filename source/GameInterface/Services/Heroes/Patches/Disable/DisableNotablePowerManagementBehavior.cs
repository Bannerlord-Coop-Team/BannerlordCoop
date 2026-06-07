using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(NotablePowerManagementBehavior))]
internal class DisableNotablePowerManagementBehavior
{
    [HarmonyPatch(nameof(NotablePowerManagementBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
