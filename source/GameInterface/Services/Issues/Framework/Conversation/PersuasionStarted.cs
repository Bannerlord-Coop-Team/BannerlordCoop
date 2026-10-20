using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.Conversation;

[ProtoContract(SkipConstructor = true)]
public readonly struct PersuasionStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly string Generation;

    [ProtoMember(4)]
    public readonly string TargetId;

    public PersuasionStarted(string issueOwnerId, string issueId, string generation, string targetId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Generation = generation;
        TargetId = targetId;
    }
}
