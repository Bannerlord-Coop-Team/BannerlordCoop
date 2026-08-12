using System.Collections.Generic;

namespace GameInterface.Services.Issues.Generic;

public interface IIssueConversationTracker
{
    void Register(string issueGiverId, string controllerId, int generation);
    bool TryGetTrackedRequester(string issueGiverId, out string controllerId, out int generation);
}

internal sealed class IssueConversationTracker : IIssueConversationTracker
{
    private readonly Dictionary<string, (string ControllerId, int Generation)> tracked = new();

    public void Register(string issueGiverId, string controllerId, int generation)
    {
        if (issueGiverId == null || controllerId == null) return;

        tracked[issueGiverId] = (controllerId, generation);
    }

    public bool TryGetTrackedRequester(string issueGiverId, out string controllerId, out int generation)
    {
        if (issueGiverId != null && tracked.TryGetValue(issueGiverId, out var entry))
        {
            controllerId = entry.ControllerId;
            generation = entry.Generation;
            return true;
        }

        controllerId = null;
        generation = 0;
        return false;
    }
}
