using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.OwnerChange;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueOwnerChanged : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OldOwnerId;

    [ProtoMember(2)]
    public readonly string NewOwnerId;

    [ProtoMember(3)]
    public readonly string IssueId;

    public NetworkIssueOwnerChanged(string oldOwnerId, string newOwnerId, string issueId)
    {
        OldOwnerId = oldOwnerId;
        NewOwnerId = newOwnerId;
        IssueId = issueId;
    }
}
