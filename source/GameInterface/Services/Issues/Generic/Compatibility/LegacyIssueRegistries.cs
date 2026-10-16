using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic;

public interface IIssueGenerationRegistry
{
    int Bump(Hero owner);
    void SetGeneration(Hero owner, int generation);
    bool TryGetGeneration(Hero owner, out int generation);
    IReadOnlyCollection<KeyValuePair<Hero, int>> Snapshot();
    void RestoreAll(IEnumerable<KeyValuePair<Hero, int>> entries);
}

internal sealed class IssueGenerationRegistry : IIssueGenerationRegistry
{
    private readonly PendingRegistry<int> registry = new();

    public int Bump(Hero owner)
    {
        registry.TryGet(owner, out var current);
        var next = current + 1;
        registry.Set(owner, next);
        return next;
    }

    public void SetGeneration(Hero owner, int generation) => registry.Set(owner, generation);
    public bool TryGetGeneration(Hero owner, out int generation) => registry.TryGet(owner, out generation);
    public IReadOnlyCollection<KeyValuePair<Hero, int>> Snapshot() => registry.Snapshot();
    public void RestoreAll(IEnumerable<KeyValuePair<Hero, int>> entries) => registry.RestoreAll(entries);
}

public interface IIssueOwnershipRegistry
{
    void SetOwner(Hero issueGiver, string controllerId);
    void Clear(Hero issueGiver);
    void ClearAll();
    bool TryGetOwnerControllerId(Hero issueGiver, out string controllerId);
    bool IsLocalPeerOwner(Hero issueGiver);
    IReadOnlyCollection<KeyValuePair<Hero, string>> Snapshot();
    void RestoreAll(IEnumerable<KeyValuePair<Hero, string>> entries);
}

/// <summary>Compatibility view for retained rival-quest code and tests over the current id registry.</summary>
internal sealed class LegacyIssueOwnershipRegistry : IIssueOwnershipRegistry
{
    private readonly IObjectManager objectManager;
    private readonly global::GameInterface.Services.Issues.Framework.Interface.IIssueOwnershipRegistry current;
    private readonly IControllerIdProvider controllerIdProvider;

    public LegacyIssueOwnershipRegistry(
        IObjectManager objectManager,
        global::GameInterface.Services.Issues.Framework.Interface.IIssueOwnershipRegistry current,
        IControllerIdProvider controllerIdProvider)
    {
        this.objectManager = objectManager;
        this.current = current;
        this.controllerIdProvider = controllerIdProvider;
    }

    public void SetOwner(Hero issueGiver, string controllerId)
    {
        if (TryGetKeys(issueGiver, out var ownerId, out var issueId))
            current.TrySetOwner(ownerId, issueId, controllerId);
    }

    public void Clear(Hero issueGiver)
    {
        if (objectManager.TryGetId(issueGiver, out var ownerId)) current.Remove(ownerId);
    }

    public void ClearAll()
    {
        foreach (var entry in current.GetAll()) current.Remove(entry.IssueOwnerId);
    }

    public bool TryGetOwnerControllerId(Hero issueGiver, out string controllerId)
    {
        controllerId = null;
        return TryGetKeys(issueGiver, out var ownerId, out var issueId) &&
            current.TryGetOwner(ownerId, issueId, out controllerId);
    }

    public bool IsLocalPeerOwner(Hero issueGiver)
    {
        return TryGetOwnerControllerId(issueGiver, out var owner) &&
            owner == controllerIdProvider.ControllerId;
    }

    public IReadOnlyCollection<KeyValuePair<Hero, string>> Snapshot()
    {
        var result = new List<KeyValuePair<Hero, string>>();
        foreach (var entry in current.GetAll())
        {
            if (objectManager.TryGetObject<Hero>(entry.IssueOwnerId, out var hero))
                result.Add(new KeyValuePair<Hero, string>(hero, entry.ControllerId));
        }
        return result;
    }

    public void RestoreAll(IEnumerable<KeyValuePair<Hero, string>> entries)
    {
        ClearAll();
        if (entries == null) return;
        foreach (var entry in entries) SetOwner(entry.Key, entry.Value);
    }

    private bool TryGetKeys(Hero issueGiver, out string ownerId, out string issueId)
    {
        ownerId = null;
        issueId = null;
        return issueGiver != null && issueGiver.Issue != null &&
            objectManager.TryGetId(issueGiver, out ownerId) &&
            (issueId = issueGiver.Issue.StringId) != null;
    }
}
