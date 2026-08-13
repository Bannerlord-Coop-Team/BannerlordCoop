using GameInterface.Services.Issues.Generic;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class IssueOwnershipPersistencePatches
{
    private const string SaveKey = "_coop_issue_ownership";
    private const string GenerationSaveKey = "_coop_issue_generation";

    [HarmonyPatch(nameof(IssuesCampaignBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry)) return;
        if (!ContainerProvider.TryResolve<IIssueGenerationRegistry>(out var generationRegistry)) return;

        List<IssueOwnershipSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = ownershipRegistry.Snapshot()
                .Select(kvp => new IssueOwnershipSaveData(kvp.Key, kvp.Value))
                .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (dataStore.IsLoading)
        {
            if (saveData == null)
            {
                ownershipRegistry.ClearAll();
            }
            else
            {
                ownershipRegistry.RestoreAll(saveData
                    .Where(entry => entry?.IssueGiverHero != null && !string.IsNullOrEmpty(entry.OwnerControllerId))
                    .Select(entry => new KeyValuePair<Hero, string>(entry.IssueGiverHero, entry.OwnerControllerId)));
            }
        }

        List<IssueGenerationSaveData> generationSaveData = null;
        if (dataStore.IsSaving)
        {
            generationSaveData = generationRegistry.Snapshot()
                .Select(kvp => new IssueGenerationSaveData(kvp.Key, kvp.Value))
                .ToList();
        }

        dataStore.SyncData(GenerationSaveKey, ref generationSaveData);
        if (!dataStore.IsLoading) return;

        generationRegistry.RestoreAll((generationSaveData ?? new List<IssueGenerationSaveData>())
            .Where(entry => entry?.IssueGiverHero != null)
            .Select(entry => new KeyValuePair<Hero, int>(entry.IssueGiverHero, entry.Generation)));
    }
}
