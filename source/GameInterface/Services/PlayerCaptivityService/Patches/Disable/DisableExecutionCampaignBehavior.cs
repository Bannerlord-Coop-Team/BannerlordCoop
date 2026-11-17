using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.PlayerCaptivityService.Patches.Disable;

/// <summary>
/// Stops executions, the only source of blood feuds.
/// Remove it together with <see cref="Clans.Patches.BloodFeudRepairPatch"/> once #3851 tracks feuds per player.
/// </summary>
[HarmonyPatch(typeof(ExecutionCampaignBehavior), nameof(ExecutionCampaignBehavior.RegisterEvents))]
internal class DisableExecutionCampaignBehavior
{
    static bool Prefix() => false;
}
