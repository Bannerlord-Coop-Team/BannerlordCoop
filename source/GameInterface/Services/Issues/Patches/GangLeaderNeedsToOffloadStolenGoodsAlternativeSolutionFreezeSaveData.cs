using Common.Logging;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Patches;

internal sealed class GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezeSaveData
{
    [SaveableField(1)]
    internal Hero IssueOwner;

    [SaveableField(2)]
    internal int StolenTradeGoodAmount;

    [SaveableField(3)]
    internal int RewardGold;

    private GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezeSaveData()
    {
    }

    internal GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezeSaveData(Hero issueOwner, int stolenTradeGoodAmount, int rewardGold)
    {
        IssueOwner = issueOwner;
        StolenTradeGoodAmount = stolenTradeGoodAmount;
        RewardGold = rewardGold;
    }
}

public sealed class GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezeSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_190_000;

    public GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezeSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezeSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezeSaveData>));
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezePersistencePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezePersistencePatches>();

    private const string SaveKey = "_coop_ganglead_stolengoods_altsolution_freeze";

    [HarmonyPatch(nameof(IssuesCampaignBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        try
        {
            SyncDataInternal(dataStore);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to sync GangLeaderNeedsToOffloadStolenGoods alternative-solution freeze save data - registry may be left partially restored");
        }
    }

    private static void SyncDataInternal(IDataStore dataStore)
    {
        var freeze = GangLeaderNeedsToOffloadStolenGoodsQuestType.AlternativeSolutionFreeze;

        List<GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezeSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = freeze.Snapshot()
                .Select(kvp => new GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionFreezeSaveData(
                    kvp.Key, kvp.Value.StolenTradeGoodAmount, kvp.Value.RewardGold))
                .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (!dataStore.IsLoading) return;

        freeze.ClearAll();
        if (saveData == null) return;

        freeze.RestoreAll(saveData
            .Where(entry => entry?.IssueOwner != null)
            .Select(entry => new KeyValuePair<Hero, (int StolenTradeGoodAmount, int RewardGold)>(
                entry.IssueOwner, (entry.StolenTradeGoodAmount, entry.RewardGold))));
    }
}
