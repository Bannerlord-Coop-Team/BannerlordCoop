using GameInterface.Services.Issues.Framework.Interface;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Issues.Framework.Registries;

internal class IssueOwnershipRegistry : IIssueOwnershipRegistry
{
    private readonly Dictionary<string, (string IssueId, string ControllerId)> owners =
        new Dictionary<string, (string IssueId, string ControllerId)>();
    // Keep the issue-to-owner tombstone after Remove so finalization can still
    // route the terminal journal when the live owner registry entry is gone.
    private readonly Dictionary<string, string> issueOwners = new Dictionary<string, string>();

    public bool TrySetOwner(string issueOwnerId, string issueId, string controllerId)
    {
        lock (owners)
        {
            if (owners.TryGetValue(issueOwnerId, out var existing) && existing.IssueId == issueId)
            {
                return existing.ControllerId == controllerId;
            }

            owners[issueOwnerId] = (issueId, controllerId);
            issueOwners[issueId] = issueOwnerId;
            return true;
        }
    }

    public bool TryGetIssueOwnerId(string issueId, out string issueOwnerId)
    {
        lock (owners) return issueOwners.TryGetValue(issueId, out issueOwnerId);
    }

    public bool TryGetOwner(string issueOwnerId, string issueId, out string controllerId)
    {
        controllerId = null;

        lock (owners)
        {
            if (!owners.TryGetValue(issueOwnerId, out var existing) || existing.IssueId != issueId)
            {
                return false;
            }

            controllerId = existing.ControllerId;
            return true;
        }
    }

    public void Remove(string issueOwnerId)
    {
        lock (owners)
        {
            owners.Remove(issueOwnerId);
        }
    }

    public IssueOwnershipData[] GetAll()
    {
        lock (owners)
        {
            return owners
                .Select(owner => new IssueOwnershipData(owner.Key, owner.Value.IssueId, owner.Value.ControllerId))
                .ToArray();
        }
    }
}
