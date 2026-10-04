using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Patches;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace GameInterface.Services.Issues;

using Issue = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;
using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

internal interface IExtortionQuestJournal
{
    void AssignOwner(Hero giver, string controllerId);
    bool IsVisible(QuestItemVM item);
    void SyncData(IDataStore dataStore);
}

internal sealed class ExtortionQuestJournal : IExtortionQuestJournal
{
    private readonly Dictionary<string, string> owners = new();
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IControllerIdProvider controller;

    public ExtortionQuestJournal(IIssueOwnershipRegistry ownership, IControllerIdProvider controller)
    {
        this.ownership = ownership;
        this.controller = controller;
    }

    public void AssignOwner(Hero giver, string controllerId)
    {
        if (giver?.Issue is not Issue issue) return;
        owners[issue.StringId] = controllerId;
    }

    public void SyncData(IDataStore dataStore)
    {
        List<IssueOwnershipSaveData> entries = dataStore.IsSaving
            ? owners.Select(pair => new IssueOwnershipSaveData(null, pair.Value, pair.Key)).ToList() : null;
        dataStore.SyncData("_coop_extortion_journal_owners", ref entries);
        if (dataStore.IsLoading) Restore(entries);
    }

    internal void Restore(IEnumerable<IssueOwnershipSaveData> entries)
    {
        owners.Clear();
        if (entries == null) return;
        foreach (var entry in entries)
            if (!string.IsNullOrEmpty(entry?.IssueId) && !string.IsNullOrEmpty(entry.OwnerControllerId))
                owners[entry.IssueId] = entry.OwnerControllerId;
    }

    public bool IsVisible(QuestItemVM item)
    {
        if (item.Quest is Quest quest) return ownership.IsLocalPeerOwner(quest.QuestGiver);
        if (item.Issue is Issue issue) return ownership.IsLocalPeerOwner(issue.IssueOwner);
        return IsVisible(item.QuestLogEntry?._relatedObjectIds);
    }

    internal bool IsVisible(IEnumerable<string> relatedIds)
    {
        if (relatedIds == null) return true;
        foreach (var id in relatedIds)
            if (owners.TryGetValue(id, out var owner)) return owner == controller.ControllerId;
        return true;
    }
}
