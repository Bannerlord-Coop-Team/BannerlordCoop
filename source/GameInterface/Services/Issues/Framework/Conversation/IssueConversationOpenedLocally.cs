using Common.Messaging;

namespace GameInterface.Services.Issues.Framework.Conversation;

public readonly struct IssueConversationOpenedLocally : IEvent
{
    public readonly string IssueOwnerId;

    public IssueConversationOpenedLocally(string issueOwnerId)
    {
        IssueOwnerId = issueOwnerId;
    }
}
