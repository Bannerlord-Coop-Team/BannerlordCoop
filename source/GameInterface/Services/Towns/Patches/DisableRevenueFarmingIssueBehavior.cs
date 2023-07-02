using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(RevenueFarmingIssueBehavior))]
internal class DisableRevenueFarmingIssueBehavior
{
    [HarmonyPatch(nameof(RevenueFarmingIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
