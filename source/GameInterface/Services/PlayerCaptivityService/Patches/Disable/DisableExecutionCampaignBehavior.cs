using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.PlayerCaptivityService.Patches.Disable;

/// <summary>
/// ExecutionCampaignBehavior rolls whether a captured main hero is executed. Its handlers are keyed on Hero.MainHero and
/// Clan.PlayerClan, so on a client they would decide executions and feuds for that client alone.
/// The server owns them; blood feuds between players are not multiplayer-aware yet.
/// </summary>
[HarmonyPatch(typeof(ExecutionCampaignBehavior), nameof(ExecutionCampaignBehavior.RegisterEvents))]
internal class DisableExecutionCampaignBehavior
{
    static bool Prefix() => ModInformation.IsServer;
}
