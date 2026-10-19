using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.Conversation;

public readonly struct IssueConversationOpenedLocally : IEvent
{
    public readonly string IssueOwnerId;

    public IssueConversationOpenedLocally(string issueOwnerId)
    {
        IssueOwnerId = issueOwnerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestIssueConversationOpened : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;

    public RequestIssueConversationOpened(string issueOwnerId)
    {
        IssueOwnerId = issueOwnerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueConversationAllowed : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;

    public NetworkIssueConversationAllowed(string issueOwnerId, string issueId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueConversationDenied : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;

    public NetworkIssueConversationDenied(string issueOwnerId)
    {
        IssueOwnerId = issueOwnerId;
    }
}
