using GameInterface.Services.Entity;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

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
    void SetQuestOwner(string questId, string controllerId);
    bool TryGetQuestOwner(string questId, out string controllerId);
    IReadOnlyCollection<KeyValuePair<string, string>> SnapshotQuests();
    void RestoreQuests(IEnumerable<KeyValuePair<string, string>> entries);
}

internal sealed class IssueOwnershipRegistry : IIssueOwnershipRegistry
{
    private readonly PendingRegistry<string> registry = new();
    private readonly ConcurrentDictionary<string, string> questOwners = new();

    public void SetOwner(Hero issueGiver, string controllerId)
    {
        if (issueGiver == null || string.IsNullOrEmpty(controllerId)) return;

        registry.Set(issueGiver, controllerId);
    }

    public void Clear(Hero issueGiver)
    {
        registry.Clear(issueGiver);
    }

    public void ClearAll()
    {
        registry.ClearAll();
        questOwners.Clear();
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

    public void SetQuestOwner(string questId, string controllerId)
    {
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(controllerId)) return;
        questOwners[questId] = controllerId;
    }

    public bool TryGetQuestOwner(string questId, out string controllerId)
    {
        controllerId = null;
        return questId != null && questOwners.TryGetValue(questId, out controllerId);
    }

    public IReadOnlyCollection<KeyValuePair<string, string>> SnapshotQuests() => questOwners.ToArray();

    public void RestoreQuests(IEnumerable<KeyValuePair<string, string>> entries)
    {
        questOwners.Clear();
        foreach (var entry in entries) SetQuestOwner(entry.Key, entry.Value);
    }
}
