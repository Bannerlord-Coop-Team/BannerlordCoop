using GameInterface.Services.Entity;
using System.Collections.Generic;
using System.Linq;
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
    void MigrateControllerId(string legacyControllerId, string controllerId);
}

internal sealed class IssueOwnershipRegistry : IIssueOwnershipRegistry
{
    private readonly PendingRegistry<string> registry = new();

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

    public void MigrateControllerId(string legacyControllerId, string controllerId)
    {
        if (string.IsNullOrEmpty(legacyControllerId) ||
            string.IsNullOrEmpty(controllerId) ||
            legacyControllerId == controllerId)
            return;

        registry.RestoreAll(registry.Snapshot().Select(entry =>
            new KeyValuePair<Hero, string>(
                entry.Key,
                entry.Value == legacyControllerId ? controllerId : entry.Value)));
    }
}
