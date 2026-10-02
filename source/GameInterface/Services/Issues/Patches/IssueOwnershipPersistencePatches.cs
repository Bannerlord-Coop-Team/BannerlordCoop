using Common.Logging;
using GameInterface.Services.Issues.Generic;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class IssueOwnershipPersistencePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(IssueOwnershipPersistencePatches));

    private const string SaveKey = "_coop_issue_ownership";
    private const string GenerationSaveKey = "_coop_issue_generation";
    private const string QuestOwnershipSaveKey = "_coop_quest_ownership";

    [HarmonyPatch(nameof(IssuesCampaignBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry)) return;
        if (!ContainerProvider.TryResolve<IIssueGenerationRegistry>(out var generationRegistry)) return;

        try
        {
            SyncDataInternal(dataStore, ownershipRegistry, generationRegistry);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to sync issue ownership/generation save data - registries may be left partially restored");
        }
    }

    private static void SyncDataInternal(IDataStore dataStore, IIssueOwnershipRegistry ownershipRegistry, IIssueGenerationRegistry generationRegistry)
    {
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

        List<QuestOwnershipSaveData> questOwners = null;
        if (dataStore.IsSaving)
        {
            questOwners = ownershipRegistry.SnapshotQuests()
                .Select(entry => new QuestOwnershipSaveData(entry.Key, entry.Value)).ToList();
        }
        dataStore.SyncData(QuestOwnershipSaveKey, ref questOwners);
        if (dataStore.IsLoading)
        {
            ownershipRegistry.RestoreQuests((questOwners ?? new List<QuestOwnershipSaveData>())
                .Where(entry => entry != null)
                .Select(entry => new KeyValuePair<string, string>(entry.QuestId, entry.OwnerControllerId)));
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
