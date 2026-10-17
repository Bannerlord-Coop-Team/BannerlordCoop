using Common.Logging;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using FrameworkOwnership = GameInterface.Services.Issues.Framework.Interface.IIssueOwnershipRegistry;

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

    // Records every owner in the framework's ownership registry again, for owners restored before their objects had ids
    void MirrorAll();
}

/// <summary>
/// Remembers which player took each migrated quest and tells the framework's ownership registry, so the framework's
/// owner lookup, similar commitments gate and accept preconditions see these quests like any other.
/// </summary>
internal sealed class IssueOwnershipRegistry : IIssueOwnershipRegistry
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueOwnershipRegistry>();

    private readonly PendingRegistry<string> registry = new();
    private readonly FrameworkOwnership frameworkOwnership;
    private readonly IObjectManager objectManager;

    public IssueOwnershipRegistry(FrameworkOwnership frameworkOwnership, IObjectManager objectManager)
    {
        this.frameworkOwnership = frameworkOwnership;
        this.objectManager = objectManager;
    }

    public void SetOwner(Hero issueGiver, string controllerId)
    {
        if (issueGiver == null || string.IsNullOrEmpty(controllerId)) return;

        registry.Set(issueGiver, controllerId);
        Mirror(issueGiver, controllerId);
    }

    public void Clear(Hero issueGiver)
    {
        registry.Clear(issueGiver);
        Unmirror(issueGiver);
    }

    public void ClearAll()
    {
        foreach (var owned in registry.Snapshot()) Unmirror(owned.Key);

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
        ClearAll();
        registry.RestoreAll(entries);
        MirrorAll();
    }

    public void MirrorAll()
    {
        foreach (var owned in registry.Snapshot()) Mirror(owned.Key, owned.Value);
    }

    private void Mirror(Hero issueGiver, string controllerId)
    {
        var issue = issueGiver.Issue;
        if (issue == null || !objectManager.TryGetId(issueGiver, out var issueGiverId))
        {
            Logger.Debug("Not recording the owner of {hero} with the framework yet, it has no issue or no id", issueGiver.StringId);
            return;
        }

        // An accept replaces whoever the framework remembered for this giver, as it does here
        frameworkOwnership.Remove(issueGiverId);
        frameworkOwnership.TrySetOwner(issueGiverId, issue.StringId, controllerId);
    }

    private void Unmirror(Hero issueGiver)
    {
        if (issueGiver != null && objectManager.TryGetId(issueGiver, out var issueGiverId))
        {
            frameworkOwnership.Remove(issueGiverId);
        }
    }
}
