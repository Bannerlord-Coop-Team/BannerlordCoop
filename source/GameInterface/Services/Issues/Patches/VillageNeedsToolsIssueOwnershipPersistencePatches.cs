using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Persists <see cref="VillageNeedsToolsIssueOwnership"/> in the existing
/// <see cref="VillageNeedsToolsIssueBehavior"/> save record, mirroring the exact piggyback pattern already
/// used by <c>PlayerCaptivityAttackProtectionPersistencePatches</c>.
///
/// Unlike that precedent (which is server-owned-only data a client never independently holds), this
/// registry is symmetric: every peer populates its own copy identically from the server's broadcast, so every
/// peer just saves/restores its own copy as-is - no
/// client/server special-casing needed. This is also what makes join and reconnect both come out correct:
/// per <c>Coop.Core.Server.Connections.States.TransferSaveState</c>, every (re)connection is serviced by the
/// server calling <c>SaveCurrentGame()</c> on its own live state and shipping that snapshot to the
/// connecting peer - so as long as the server's own registry entry for a hero is correct (which it always
/// is, since it's set from the exact same broadcast every other peer used), a reconnecting owner gets their
/// own correct ControllerId back after resync.
/// </summary>
[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior))]
internal class VillageNeedsToolsIssueOwnershipPersistencePatches
{
    private const string SaveKey = "_coop_village_needs_tools_issue_ownership";

    [HarmonyPatch(nameof(VillageNeedsToolsIssueBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        List<VillageNeedsToolsIssueOwnershipSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = VillageNeedsToolsIssueOwnership.Snapshot()
                .Select(kvp => new VillageNeedsToolsIssueOwnershipSaveData(kvp.Key, kvp.Value))
                .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (!dataStore.IsLoading) return;

        VillageNeedsToolsIssueOwnership.ClearAll();
        if (saveData == null) return;

        foreach (var entry in saveData)
        {
            if (entry?.IssueGiverHero == null || string.IsNullOrEmpty(entry.OwnerControllerId)) continue;

            VillageNeedsToolsIssueOwnership.SetOwner(entry.IssueGiverHero, entry.OwnerControllerId);
        }
    }
}
