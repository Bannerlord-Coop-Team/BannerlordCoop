using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LandLordNeedsManualLaborersIssueBehavior))]
internal class DisableLandLordNeedsManualLaborersIssueBehavior
{
    [HarmonyPatch(nameof(LandLordNeedsManualLaborersIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
