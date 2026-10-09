using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkQuestSolutionAccepted : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly string ControllerId;

    [ProtoMember(4)]
    public readonly float DifficultyMultiplier;

    [ProtoMember(5)]
    public readonly byte[] Captured;

    public NetworkQuestSolutionAccepted(
        string issueOwnerId,
        string issueId,
        string controllerId,
        float difficultyMultiplier,
        byte[] captured)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        DifficultyMultiplier = difficultyMultiplier;
        Captured = captured;
    }
}
