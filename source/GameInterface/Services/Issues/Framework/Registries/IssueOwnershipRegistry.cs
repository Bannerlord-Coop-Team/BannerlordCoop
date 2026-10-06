using GameInterface.Services.Issues.Framework.Interface;
using System.Collections.Generic;

namespace GameInterface.Services.Issues.Framework.Registries;

internal class IssueOwnershipRegistry : IIssueOwnershipRegistry
{
    private readonly Dictionary<string, (string IssueId, string ControllerId)> owners =
        new Dictionary<string, (string IssueId, string ControllerId)>();

    public bool TrySetOwner(string issueOwnerId, string issueId, string controllerId)
    {
        lock (owners)
        {
            if (owners.TryGetValue(issueOwnerId, out var existing) && existing.IssueId == issueId)
            {
                return existing.ControllerId == controllerId;
            }

            owners[issueOwnerId] = (issueId, controllerId);
            return true;
        }
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
}
