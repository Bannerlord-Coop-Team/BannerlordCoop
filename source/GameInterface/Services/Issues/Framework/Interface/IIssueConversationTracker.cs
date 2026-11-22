namespace GameInterface.Services.Issues.Framework.Interface;

internal interface IIssueConversationTracker
{
    void Register(string issueOwnerId, string controllerId, string issueId);
    bool TryGet(string issueOwnerId, string controllerId, string issueId);
}
