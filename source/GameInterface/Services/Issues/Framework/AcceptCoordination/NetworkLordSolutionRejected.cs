using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>Sent when the server rejects a lord-solution request without finalizing the issue.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLordSolutionRejected : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    public NetworkLordSolutionRejected(string issueOwnerId, string issueId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
    }
}
