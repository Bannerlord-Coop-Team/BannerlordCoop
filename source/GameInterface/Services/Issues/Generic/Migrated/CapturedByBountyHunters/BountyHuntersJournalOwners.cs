using System.Collections.Generic;
using System.Linq;
using GameInterface.Services.Entity;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;

namespace GameInterface.Services.Issues.Generic.Migrated.CapturedByBountyHunters;

using Issue = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;
using Quest = CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

internal interface IBountyHuntersJournalOwners
{
    void Record(Issue issue, string controllerId);
    void SyncData(IDataStore dataStore);
    void FilterLoadedCampaign();
}

internal sealed class BountyHuntersJournalOwners : IBountyHuntersJournalOwners
{
    private Dictionary<string, string> owners = new();
    private readonly IControllerIdProvider controller;
    private readonly IIssueOwnershipRegistry ownership;

    public BountyHuntersJournalOwners(IControllerIdProvider controller, IIssueOwnershipRegistry ownership)
    {
        this.controller = controller;
        this.ownership = ownership;
    }

    public void Record(Issue issue, string controllerId)
    {
        owners[issue.StringId] = controllerId;
        if (issue.IssueQuest != null) owners[issue.IssueQuest.StringId] = controllerId;
    }

    public void SyncData(IDataStore dataStore)
    {
        var saved = dataStore.IsSaving ? owners : null;
        dataStore.SyncData("_coop_bounty_hunters_journal_owners", ref saved);
        if (dataStore.IsLoading) owners = saved ?? new Dictionary<string, string>();
    }

    public void FilterLoadedCampaign()
    {
        var campaign = Campaign.Current;
        foreach (var quest in campaign.QuestManager.Quests.OfType<Quest>().ToArray())
        {
            if (ownership.IsLocalPeerOwner(quest.QuestGiver)) continue;
            quest.ClearRelatedFields();
            quest.RemoveAllTrackedObjects();
            quest.RemoveAllMapMarkers();
            campaign.QuestManager.OnQuestFinalized(quest);
            if (quest.QuestGiver.Issue is Issue issue) issue.IssueQuest = null;
        }
        foreach (var issue in campaign.IssueManager.Issues.Values.OfType<Issue>())
        {
            if (!ownership.IsLocalPeerOwner(issue.IssueOwner)) issue._journalEntries.Clear();
        }
        var logs = campaign.LogEntryHistory._logs;
        for (var i = logs.Count - 1; i >= 0; i--)
        {
            if (logs[i] is JournalLogEntry journal && journal._relatedObjectIds.Any(id =>
                owners.TryGetValue(id, out var owner) && owner != controller.ControllerId))
            {
                logs.RemoveAt(i);
            }
        }
    }
}
