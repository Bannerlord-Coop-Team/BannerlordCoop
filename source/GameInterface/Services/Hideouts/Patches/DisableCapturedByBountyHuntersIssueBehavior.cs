using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Hideouts.Patches;

[HarmonyPatch(typeof(CapturedByBountyHuntersIssueBehavior))]
internal class DisableCapturedByBountyHuntersIssueBehavior
{
    [HarmonyPatch(nameof(CapturedByBountyHuntersIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
