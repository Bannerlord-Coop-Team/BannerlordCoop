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

    [HarmonyPatch(nameof(IssuesCampaignBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        List<IssueOwnershipSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = IssueOwnershipRegistry.Snapshot()
                .Select(kvp => new IssueOwnershipSaveData(kvp.Key, kvp.Value))
                .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (!dataStore.IsLoading) return;

        if (saveData == null)
        {
            IssueOwnershipRegistry.ClearAll();
            return;
        }

        IssueOwnershipRegistry.RestoreAll(saveData
            .Where(entry => entry?.IssueGiverHero != null && !string.IsNullOrEmpty(entry.OwnerControllerId))
            .Select(entry => new KeyValuePair<Hero, string>(entry.IssueGiverHero, entry.OwnerControllerId)));
    }
}
