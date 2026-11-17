using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.PlayerCaptivityService.Patches.Disable;

/// <summary>
/// Stops executions, the only source of blood feuds; <see cref="Clans.Patches.BloodFeudRepairPatch"/> wipes saved feuds on load, so remove both together.
/// </summary>
[HarmonyPatch(typeof(ExecutionCampaignBehavior), nameof(ExecutionCampaignBehavior.RegisterEvents))]
internal class DisableExecutionCampaignBehavior
{
    static bool Prefix() => false;
}
