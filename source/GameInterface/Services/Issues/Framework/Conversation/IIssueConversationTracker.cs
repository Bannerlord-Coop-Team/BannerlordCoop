using System.Linq;

namespace GameInterface.Services.Issues.Framework.Conversation;

internal interface IIssueConversationTracker
{
    void Register(string issueOwnerId, string controllerId, string issueId);
    bool TryGet(string issueOwnerId, string controllerId, string issueId);
    void Clear(string issueOwnerId);
}

internal sealed class IssueConversationTracker : IIssueConversationTracker
{
    private readonly System.Collections.Generic.Dictionary<(string OwnerId, string ControllerId), string> tracked = new();

    public void Register(string issueOwnerId, string controllerId, string issueId)
    {
        if (string.IsNullOrEmpty(issueOwnerId) || string.IsNullOrEmpty(controllerId) || string.IsNullOrEmpty(issueId)) return;
        tracked[(issueOwnerId, controllerId)] = issueId;
    }

    public bool TryGet(string issueOwnerId, string controllerId, string issueId)
    {
        return issueOwnerId != null && controllerId != null && issueId != null &&
            tracked.TryGetValue((issueOwnerId, controllerId), out var trackedIssueId) && trackedIssueId == issueId;
    }

    public void Clear(string issueOwnerId)
    {
        if (issueOwnerId == null) return;
        foreach (var key in tracked.Keys.Where(key => key.OwnerId == issueOwnerId).ToArray()) tracked.Remove(key);
    }
}
