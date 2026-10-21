using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// Sent to a client whose alternative solution accept was turned down, its troops go back to its party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAlternativeSolutionAcceptRejected : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;
    [ProtoMember(3)] public readonly string Reason;

    public NetworkAlternativeSolutionAcceptRejected(string issueOwnerId, string issueId, string reason = null)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Reason = reason;
    }
}
