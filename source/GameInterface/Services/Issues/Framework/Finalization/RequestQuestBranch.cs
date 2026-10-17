using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.Finalization;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestQuestBranch : ICommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly byte Proof;

    public RequestQuestBranch(string issueOwnerId, string issueId, byte proof)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Proof = proof;
    }
}
