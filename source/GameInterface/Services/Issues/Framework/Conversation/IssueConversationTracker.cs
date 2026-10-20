using GameInterface.Services.Issues.Framework.Interface;

namespace GameInterface.Services.Issues.Framework.Conversation;

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
}
