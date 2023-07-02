using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(LadysKnightOutIssueBehavior))]
internal class DisableLadysKnightOutIssueBehavior
{
    [HarmonyPatch(nameof(LadysKnightOutIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
