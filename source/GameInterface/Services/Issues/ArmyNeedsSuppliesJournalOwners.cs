using System.Collections.Generic;
using System.Linq;
using GameInterface.Services.Entity;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues;

internal interface IArmyNeedsSuppliesJournalOwners
{
    void SetOwner(JournalLogEntry journal, string controllerId);
    bool IsVisible(JournalLogEntry journal);
    void SyncData(IDataStore dataStore);
}

internal sealed class ArmyNeedsSuppliesJournalOwners : IArmyNeedsSuppliesJournalOwners
{
    private readonly IControllerIdProvider controller;
    private readonly Dictionary<JournalLogEntry, string> owners = new();

    public ArmyNeedsSuppliesJournalOwners(IControllerIdProvider controller) => this.controller = controller;

    public void SetOwner(JournalLogEntry journal, string controllerId)
    {
        if (journal != null && !string.IsNullOrEmpty(controllerId)) owners[journal] = controllerId;
    }

    public bool IsVisible(JournalLogEntry journal) => journal == null ||
        !owners.TryGetValue(journal, out var owner) || owner == controller.ControllerId;

    public void SyncData(IDataStore dataStore)
    {
        List<ArmyNeedsSuppliesJournalOwnerSaveData> entries = null;
        if (dataStore.IsSaving)
        {
            var history = Campaign.Current.LogEntryHistory.GameActionLogs;
            entries = owners.Where(pair => history.Contains(pair.Key))
                .Select(pair => new ArmyNeedsSuppliesJournalOwnerSaveData(pair.Key, pair.Value)).ToList();
        }
        dataStore.SyncData("_coop_army_supply_journal_owners", ref entries);
        if (!dataStore.IsLoading) return;
        owners.Clear();
        foreach (var entry in entries ?? new List<ArmyNeedsSuppliesJournalOwnerSaveData>())
            SetOwner(entry.Journal, entry.ControllerId);
    }
}

internal sealed class ArmyNeedsSuppliesJournalOwnerSaveData
{
    [SaveableField(1)] internal JournalLogEntry Journal;
    [SaveableField(2)] internal string ControllerId;

    private ArmyNeedsSuppliesJournalOwnerSaveData() { }

    internal ArmyNeedsSuppliesJournalOwnerSaveData(JournalLogEntry journal, string controllerId)
    {
        Journal = journal;
        ControllerId = controllerId;
    }
}
