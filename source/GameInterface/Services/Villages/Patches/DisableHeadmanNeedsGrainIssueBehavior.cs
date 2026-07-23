using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior))]
internal class DisableHeadmanNeedsGrainIssueBehavior
{
    [HarmonyPatch(nameof(HeadmanNeedsGrainIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
