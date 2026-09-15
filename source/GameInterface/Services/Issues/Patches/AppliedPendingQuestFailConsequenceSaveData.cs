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

internal sealed class AppliedPendingQuestFailConsequenceSaveData
{
    [SaveableField(1)]
    internal long ObligationId;

    private AppliedPendingQuestFailConsequenceSaveData()
    {
    }

    internal AppliedPendingQuestFailConsequenceSaveData(long obligationId)
    {
        ObligationId = obligationId;
    }
}

public sealed class AppliedPendingQuestFailConsequenceSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_189_000;

    public AppliedPendingQuestFailConsequenceSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(AppliedPendingQuestFailConsequenceSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<AppliedPendingQuestFailConsequenceSaveData>));
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class AppliedPendingQuestFailConsequencePersistencePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<AppliedPendingQuestFailConsequencePersistencePatches>();

    private const string SaveKey = "_coop_applied_pending_quest_fail_consequence";

    [HarmonyPatch(nameof(IssuesCampaignBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        if (!ContainerProvider.TryResolve<IAppliedPendingQuestFailConsequenceTracker>(out var appliedConsequenceTracker)) return;

        try
        {
            SyncDataInternal(dataStore, appliedConsequenceTracker);
        }
        catch (System.Exception e)
        {
            Logger.Error(e, "Failed to sync applied-pending-quest-fail-consequence save data - tracker may be left partially restored");
        }
    }

    private static void SyncDataInternal(IDataStore dataStore, IAppliedPendingQuestFailConsequenceTracker appliedConsequenceTracker)
    {
        List<AppliedPendingQuestFailConsequenceSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = appliedConsequenceTracker.Snapshot()
                .Select(id => new AppliedPendingQuestFailConsequenceSaveData(id))
                .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (!dataStore.IsLoading) return;

        appliedConsequenceTracker.ClearAll();
        if (saveData == null) return;

        foreach (var entry in saveData)
        {
            if (entry == null) continue;
            appliedConsequenceTracker.Restore(entry.ObligationId);
        }
    }
}
