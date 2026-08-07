using Common;
using Common.Logging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Patches;

internal sealed class AwaitingAlternativeSolutionTroopsSaveData
{
    [SaveableField(1)]
    internal string OwnerControllerId;

    [SaveableField(2)]
    internal TroopRoster Troops;

    private AwaitingAlternativeSolutionTroopsSaveData()
    {
    }

    internal AwaitingAlternativeSolutionTroopsSaveData(string ownerControllerId, TroopRoster troops)
    {
        OwnerControllerId = ownerControllerId;
        Troops = troops;
    }
}

/// <summary>Base id 44_187_000 - must stay unique among this project's SaveableTypeDefiners.</summary>
public sealed class AwaitingAlternativeSolutionTroopsSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_187_000;

    public AwaitingAlternativeSolutionTroopsSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(AwaitingAlternativeSolutionTroopsSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<AwaitingAlternativeSolutionTroopsSaveData>));
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class AwaitingAlternativeSolutionTroopsPersistencePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<AwaitingAlternativeSolutionTroopsPersistencePatches>();

    private const string SaveKey = "_coop_awaiting_alternative_solution_troops";

    private static readonly System.Reflection.FieldInfo LegacyAwaitingTroopsField =
        AccessTools.Field(typeof(IssueManager), "_awaitingAlternativeSolutionTroops");

    [HarmonyPatch(nameof(IssuesCampaignBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        List<AwaitingAlternativeSolutionTroopsSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            saveData = AwaitingAlternativeSolutionTroopsRegistry.Snapshot()
                .Select(e => new AwaitingAlternativeSolutionTroopsSaveData(e.OwnerControllerId, e.Troops))
                .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (!dataStore.IsLoading) return;

        AwaitingAlternativeSolutionTroopsRegistry.ClearAll();
        if (saveData != null)
        {
            foreach (var entry in saveData)
            {
                if (entry?.OwnerControllerId == null || entry.Troops == null) continue;
                AwaitingAlternativeSolutionTroopsRegistry.Restore(entry.OwnerControllerId, entry.Troops);
            }
        }

        MigrateLegacyField();
    }

    private static void MigrateLegacyField()
    {
        if (ModInformation.IsClient) return;
        if (Campaign.Current?.IssueManager == null) return;

        var legacyRoster = (TroopRoster)LegacyAwaitingTroopsField.GetValue(Campaign.Current.IssueManager);
        if (legacyRoster == null || legacyRoster.Count == 0) return;

        if (ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)
            && !string.IsNullOrEmpty(controllerIdProvider.ControllerId))
        {
            Logger.Warning(
                "Migrating {Count} legacy IssueManager._awaitingAlternativeSolutionTroops troop stack(s) " +
                "(no per-owner attribution - likely a single-player-imported or pre-fix save) onto the " +
                "server's own ControllerId {ControllerId}.", legacyRoster.Count, controllerIdProvider.ControllerId);

            AwaitingAlternativeSolutionTroopsRegistry.Deposit(controllerIdProvider.ControllerId, legacyRoster);
        }
        else
        {
            Logger.Error(
                "Dropping {Count} legacy IssueManager._awaitingAlternativeSolutionTroops troop stack(s) - no " +
                "local ControllerId available to attribute them to.", legacyRoster.Count);
        }

        LegacyAwaitingTroopsField.SetValue(Campaign.Current.IssueManager, TroopRoster.CreateDummyTroopRoster());
    }
}
