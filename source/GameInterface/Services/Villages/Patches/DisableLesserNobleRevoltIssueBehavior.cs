using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LesserNobleRevoltIssueBehavior))]
internal class DisableLesserNobleRevoltIssueBehavior
{
    [HarmonyPatch(nameof(LesserNobleRevoltIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
