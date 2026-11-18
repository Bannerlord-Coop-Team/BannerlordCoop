using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Rafts.Patches;

/// <summary>
/// Only the server starts or ends a raft; clients get the synced IsInRaftState.
/// </summary>
/// <remarks>
/// Patches the public entry points instead of ApplyInternal, otherwise ApplyInternal is compiled before the AutoSync
/// _isDisabled patches and inlines the unpatched EnableAi/DisableAi, so a raft's AI state never reaches clients.
/// </remarks>
[HarmonyPatch(typeof(RaftStateChangeAction))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class RaftStateChangeActionPatches
{
    // Skips starting a raft on clients
    [HarmonyPatch(nameof(RaftStateChangeAction.ActivateRaftStateForParty))]
    [HarmonyPrefix]
    private static bool ActivateRaftStateForPartyPrefix() => ModInformation.IsServer;

    // Skips ending a raft on clients, otherwise a client's own landing transition (FinishNavigationTransitionInternal) ends it locally
    [HarmonyPatch(nameof(RaftStateChangeAction.DeactivateRaftStateForParty))]
    [HarmonyPrefix]
    private static bool DeactivateRaftStateForPartyPrefix() => ModInformation.IsServer;
}
