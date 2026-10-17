using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.Finalization;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestQuestOutcome : ICommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly IssueOutcome Outcome;

    public RequestQuestOutcome(string issueOwnerId, string issueId, IssueOutcome outcome)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Outcome = outcome;
    }
}
