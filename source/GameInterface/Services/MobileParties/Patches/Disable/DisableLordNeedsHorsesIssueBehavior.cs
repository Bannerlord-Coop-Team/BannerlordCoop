using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(LordNeedsHorsesIssueBehavior))]
internal class DisableLordNeedsHorsesIssueBehavior
{
    [HarmonyPatch(nameof(LordNeedsHorsesIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
