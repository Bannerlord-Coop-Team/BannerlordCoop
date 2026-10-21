using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.Conversation;

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
