using System.Collections.Generic;

namespace GameInterface.Services.Issues.Generic;

public interface IIssueConversationTracker
{
    void Register(string issueGiverId, string controllerId, int generation);
    bool TryGetTrackedRequester(string issueGiverId, string controllerId, out int generation);
}

internal sealed class IssueConversationTracker : IIssueConversationTracker
{
    private readonly Dictionary<(string IssueGiverId, string ControllerId), int> tracked = new();

    public void Register(string issueGiverId, string controllerId, int generation)
    {
        if (issueGiverId == null || controllerId == null) return;

        tracked[(issueGiverId, controllerId)] = generation;
    }

    public bool TryGetTrackedRequester(string issueGiverId, string controllerId, out int generation)
    {
        if (issueGiverId != null && controllerId != null && tracked.TryGetValue((issueGiverId, controllerId), out generation))
        {
            return true;
        }

        generation = 0;
        return false;
    }
}
