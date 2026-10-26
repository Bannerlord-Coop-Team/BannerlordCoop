using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

public enum LordSolutionOperation : byte
{
    Start,
    AcceptCounterOffer,
    RefuseCounterOffer,
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestLordSolution : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly LordSolutionOperation Operation;
    [ProtoMember(4)] public readonly float Difficulty;

    public RequestLordSolution(string issueOwnerId, string issueId, LordSolutionOperation operation, float difficulty = 0f)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Operation = operation;
        Difficulty = difficulty;
    }
}
