using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.Conversation;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueConversationDenied : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;

    public NetworkIssueConversationDenied(string issueOwnerId)
    {
        IssueOwnerId = issueOwnerId;
    }
}
