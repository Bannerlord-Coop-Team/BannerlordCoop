using GameInterface.Services.Entity;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;

namespace GameInterface.Services.Issues.Generic;

public interface IIssueOwnershipRegistry
{
    void SetOwner(Hero issueGiver, string controllerId);
    void Clear(Hero issueGiver);
    void ClearAll();
    bool TryGetOwnerControllerId(Hero issueGiver, out string controllerId);
    bool IsLocalPeerOwner(Hero issueGiver);
    IReadOnlyCollection<KeyValuePair<Hero, string>> Snapshot();
    void RestoreAll(IEnumerable<KeyValuePair<Hero, string>> entries);
    void SetJournalOwner(JournalLogEntry journal, string controllerId);
    bool TryGetJournalOwner(JournalLogEntry journal, out string controllerId);
    IReadOnlyCollection<KeyValuePair<JournalLogEntry, string>> JournalSnapshot();
    void RestoreJournalOwners(IEnumerable<KeyValuePair<JournalLogEntry, string>> entries);
}

internal sealed class IssueOwnershipRegistry : IIssueOwnershipRegistry
{
    private readonly PendingRegistry<string> registry = new();
    private readonly Dictionary<JournalLogEntry, string> journalOwners = new();

    public void SetOwner(Hero issueGiver, string controllerId)
    {
        if (issueGiver == null || string.IsNullOrEmpty(controllerId)) return;

        registry.Set(issueGiver, controllerId);
        if (Campaign.Current?.IssueManager != null &&
            issueGiver.Issue is ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue)
        {
            var journals = Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>();
            if (journals == null) return;
            journals.OnIssueLogAdded(issue, true);
            SetJournalOwner(journals.GetRelatedLog(issue), controllerId);
        }
    }

    public void Clear(Hero issueGiver)
    {
        registry.Clear(issueGiver);
    }

    public void ClearAll()
    {
        registry.ClearAll();
        journalOwners.Clear();
    }

    public bool TryGetOwnerControllerId(Hero issueGiver, out string controllerId)
    {
        return registry.TryGet(issueGiver, out controllerId);
    }

    public bool IsLocalPeerOwner(Hero issueGiver)
    {
        if (!TryGetOwnerControllerId(issueGiver, out var ownerControllerId)) return false;
        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return false;

        return controllerIdProvider.ControllerId == ownerControllerId;
    }

    public IReadOnlyCollection<KeyValuePair<Hero, string>> Snapshot()
    {
        return registry.Snapshot();
    }

    public void RestoreAll(IEnumerable<KeyValuePair<Hero, string>> entries)
    {
        registry.RestoreAll(entries);
    }

    public void SetJournalOwner(JournalLogEntry journal, string controllerId)
    {
        if (journal != null && !string.IsNullOrEmpty(controllerId)) journalOwners[journal] = controllerId;
    }

    public bool TryGetJournalOwner(JournalLogEntry journal, out string controllerId)
    {
        controllerId = null;
        return journal != null && journalOwners.TryGetValue(journal, out controllerId);
    }

    public IReadOnlyCollection<KeyValuePair<JournalLogEntry, string>> JournalSnapshot() => journalOwners.ToArray();

    public void RestoreJournalOwners(IEnumerable<KeyValuePair<JournalLogEntry, string>> entries)
    {
        journalOwners.Clear();
        if (entries == null) return;
        foreach (var entry in entries) SetJournalOwner(entry.Key, entry.Value);
    }
}
