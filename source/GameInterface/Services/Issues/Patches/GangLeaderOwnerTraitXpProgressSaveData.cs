using Common.Logging;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Patches;

internal sealed class GangLeaderOwnerTraitXpProgressSaveData
{
    [SaveableField(1)]
    internal Hero Owner;

    [SaveableField(2)]
    internal string TraitStringId;

    [SaveableField(3)]
    internal int XpValue;

    private GangLeaderOwnerTraitXpProgressSaveData()
    {
    }

    internal GangLeaderOwnerTraitXpProgressSaveData(Hero owner, string traitStringId, int xpValue)
    {
        Owner = owner;
        TraitStringId = traitStringId;
        XpValue = xpValue;
    }
}

public sealed class GangLeaderOwnerTraitXpProgressSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_191_000;

    public GangLeaderOwnerTraitXpProgressSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(GangLeaderOwnerTraitXpProgressSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<GangLeaderOwnerTraitXpProgressSaveData>));
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class GangLeaderOwnerTraitXpProgressPersistencePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderOwnerTraitXpProgressPersistencePatches>();

    private const string SaveKey = "_coop_ganglead_stolengoods_owner_trait_xp";

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
            Logger.Error(e, "Failed to sync GangLeaderNeedsToOffloadStolenGoods owner trait XP progress save data - runtime cache may be left partially restored");
        }
    }

    private static void SyncDataInternal(IDataStore dataStore)
    {
        var progressRegistry = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress;

        List<GangLeaderOwnerTraitXpProgressSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = progressRegistry.Snapshot()
                .SelectMany(kvp => kvp.Value.GetProperties()
                    .Select(trait => (trait, xp: kvp.Value.GetPropertyValue(trait)))
                    .Where(t => t.xp != 0)
                    .Select(t => new GangLeaderOwnerTraitXpProgressSaveData(kvp.Key, t.trait.StringId, t.xp)))
                .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (!dataStore.IsLoading) return;

        progressRegistry.ClearAll();
        if (saveData == null) return;

        var byOwner = new Dictionary<Hero, PropertyOwner<PropertyObject>>();
        foreach (var entry in saveData)
        {
            if (entry?.Owner == null || entry.TraitStringId == null) continue;
            var trait = MBObjectManager.Instance.GetObject<TraitObject>(entry.TraitStringId);
            if (trait == null) continue;

            if (!byOwner.TryGetValue(entry.Owner, out var progress))
            {
                progress = new PropertyOwner<PropertyObject>();
                byOwner[entry.Owner] = progress;
            }
            progress.SetPropertyValue(trait, entry.XpValue);
        }

        progressRegistry.RestoreAll(byOwner);
    }
}
