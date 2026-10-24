using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// Sent to a client whose accept lost to another player's.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkQuestSolutionAcceptRejected : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly long? IssueDueTimeTicks;

    public NetworkQuestSolutionAcceptRejected(string issueOwnerId, string issueId, long? issueDueTimeTicks = null)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        IssueDueTimeTicks = issueDueTimeTicks;
    }
}
