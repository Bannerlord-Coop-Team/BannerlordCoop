using GameInterface.AutoSync;
using GameInterface.Services.Rafts.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Rafts;

/// <summary>
/// Queues the raft action patch behind the AutoSync patches so the AI changes inside it still sync.
/// </summary>
internal class RaftSync : IAutoSync
{
    public RaftSync(IAutoSyncPatchCollector patchCollector)
    {
        // Queued so ApplyInternal is compiled after the AutoSync _isDisabled patches, otherwise it inlines the unpatched
        // EnableAi/DisableAi and a raft's AI state never reaches clients.
        var applyInternal = AccessTools.Method(typeof(RaftStateChangeAction), nameof(RaftStateChangeAction.ApplyInternal));
        var prefix = AccessTools.Method(typeof(RaftStateChangeActionPatches), nameof(RaftStateChangeActionPatches.ApplyInternalPrefix));
        patchCollector.AddPrefix(applyInternal, prefix);
    }
}
