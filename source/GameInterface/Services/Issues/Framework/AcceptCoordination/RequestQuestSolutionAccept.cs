using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestQuestSolutionAccept : ICommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly float DifficultyMultiplier;

    [ProtoMember(4)]
    public readonly byte[] Captured;

    public RequestQuestSolutionAccept(string issueOwnerId, string issueId, float difficultyMultiplier, byte[] captured)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        DifficultyMultiplier = difficultyMultiplier;
        Captured = captured;
    }
}
