using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

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
