using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.PlayerCaptivityService.Patches.Disable;

[HarmonyPatch(typeof(ExecutionCampaignBehavior), nameof(ExecutionCampaignBehavior.RegisterEvents))]
internal class DisableExecutionCampaignBehavior
{
    static bool Prefix() => false;
}
