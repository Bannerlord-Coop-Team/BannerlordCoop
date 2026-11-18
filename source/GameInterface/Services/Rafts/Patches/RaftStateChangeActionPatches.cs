using Common;

namespace GameInterface.Services.Rafts.Patches;

/// <summary>
/// Only the server starts or ends a raft; clients get the synced IsInRaftState.
/// Applied by <see cref="MobilePartyAIs.MobilePartyAiRegistry"/>, not by attribute.
/// </summary>
internal static class RaftStateChangeActionPatches
{
    /// <summary>
    /// Skips the action on clients, otherwise a client's own landing transition (FinishNavigationTransitionInternal) ends the
    /// party's raft locally.
    /// </summary>
    internal static bool ApplyInternalPrefix() => ModInformation.IsServer;
}
