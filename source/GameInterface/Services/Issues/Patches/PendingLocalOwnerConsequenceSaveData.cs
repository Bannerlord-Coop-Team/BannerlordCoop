using Common;
using Common.Logging;
using GameInterface.Services.Issues.Generic;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Patches;

internal sealed class PendingLocalOwnerConsequenceSaveData
{
    [SaveableField(1)]
    internal string ControllerId;

    [SaveableField(2)]
    internal string QuestTypeKey;

    [SaveableField(3)]
    internal byte Proof;

    private PendingLocalOwnerConsequenceSaveData()
    {
    }

    internal PendingLocalOwnerConsequenceSaveData(string controllerId, string questTypeKey, byte proof)
    {
        ControllerId = controllerId;
        QuestTypeKey = questTypeKey;
        Proof = proof;
    }
}

public sealed class PendingLocalOwnerConsequenceSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_188_000;

    public PendingLocalOwnerConsequenceSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(PendingLocalOwnerConsequenceSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<PendingLocalOwnerConsequenceSaveData>));
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class PendingLocalOwnerConsequencePersistencePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PendingLocalOwnerConsequencePersistencePatches>();

    private const string SaveKey = "_coop_pending_local_owner_consequence";

    [HarmonyPatch(nameof(IssuesCampaignBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        if (!ContainerProvider.TryResolve<IPendingLocalOwnerConsequenceRegistry>(out var pendingConsequenceRegistry)) return;

        try
        {
            SyncDataInternal(dataStore, pendingConsequenceRegistry);
        }
        catch (System.Exception e)
        {
            Logger.Error(e, "Failed to sync pending-local-owner-consequence save data - registry may be left partially restored");
        }
    }

    private static void SyncDataInternal(IDataStore dataStore, IPendingLocalOwnerConsequenceRegistry pendingConsequenceRegistry)
    {
        List<PendingLocalOwnerConsequenceSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = pendingConsequenceRegistry.Snapshot()
                .Select(e => new PendingLocalOwnerConsequenceSaveData(e.ControllerId, e.QuestTypeKey, e.Proof))
                .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (!dataStore.IsLoading) return;

        pendingConsequenceRegistry.ClearAll();
        if (saveData == null) return;

        foreach (var entry in saveData)
        {
            if (entry?.ControllerId == null || entry.QuestTypeKey == null) continue;
            pendingConsequenceRegistry.Restore(entry.ControllerId, entry.QuestTypeKey, entry.Proof);
        }
    }
}
