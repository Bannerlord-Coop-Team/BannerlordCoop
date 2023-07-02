using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(typeof(ArmyNeedsSuppliesIssueBehavior))]
internal class DisableArmyNeedsSuppliesIssueBehavior
{
    [HarmonyPatch(nameof(ArmyNeedsSuppliesIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
