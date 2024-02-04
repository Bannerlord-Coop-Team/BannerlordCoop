using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(NotablePowerManagementBehavior))]
internal class DisableNotablePowerManagementBehavior
{
    [HarmonyPatch(nameof(NotablePowerManagementBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
