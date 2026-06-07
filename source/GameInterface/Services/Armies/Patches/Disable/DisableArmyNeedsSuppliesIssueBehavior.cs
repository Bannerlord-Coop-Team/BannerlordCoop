using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Armies.Patches.Disable;

[HarmonyPatch(typeof(ArmyNeedsSuppliesIssueBehavior))]
internal class DisableArmyNeedsSuppliesIssueBehavior
{
    [HarmonyPatch(nameof(ArmyNeedsSuppliesIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
