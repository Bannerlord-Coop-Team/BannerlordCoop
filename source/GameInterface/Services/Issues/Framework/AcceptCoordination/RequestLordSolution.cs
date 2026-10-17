using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

internal enum LordSolutionOperation : byte
{
    Start,
    AcceptCounterOffer,
    RefuseCounterOffer,
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestLordSolution : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly LordSolutionOperation Operation;

    public RequestLordSolution(string issueOwnerId, string issueId, LordSolutionOperation operation)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Operation = operation;
    }
}
