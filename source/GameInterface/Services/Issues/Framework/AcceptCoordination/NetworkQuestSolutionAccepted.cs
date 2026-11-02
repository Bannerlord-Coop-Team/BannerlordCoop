using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkQuestSolutionAccepted : ICommand
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

    [ProtoMember(6)]
    public readonly long QuestDueTimeTicks;

    [ProtoMember(7)]
    public readonly bool HasQuestDueTime;

    public NetworkQuestSolutionAccepted(
        string issueOwnerId,
        string issueId,
        string controllerId,
        float difficultyMultiplier,
        byte[] captured,
        long questDueTimeTicks = 0,
        bool hasQuestDueTime = false)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        DifficultyMultiplier = difficultyMultiplier;
        Captured = captured;
        QuestDueTimeTicks = questDueTimeTicks;
        HasQuestDueTime = hasQuestDueTime;
    }
}
