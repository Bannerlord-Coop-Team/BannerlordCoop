using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(RevenueFarmingIssueBehavior))]
internal class DisableRevenueFarmingIssueBehavior
{
    [HarmonyPatch(nameof(RevenueFarmingIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
