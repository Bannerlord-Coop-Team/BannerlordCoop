using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Rafts.Patches;

/// <summary>
/// Only the server starts or ends a raft; clients get the synced IsInRaftState.
/// </summary>
[HarmonyPatch(typeof(RaftStateChangeAction))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class RaftStateChangeActionPatches
{
    /// <summary>
    /// Skips the action on clients, where vanilla's FinishNavigationTransitionInternal would end the raft of the party it lands.
    /// </summary>
    [HarmonyPatch(nameof(RaftStateChangeAction.ApplyInternal))]
    [HarmonyPrefix]
    private static bool ApplyInternalPrefix() => ModInformation.IsServer;
}
