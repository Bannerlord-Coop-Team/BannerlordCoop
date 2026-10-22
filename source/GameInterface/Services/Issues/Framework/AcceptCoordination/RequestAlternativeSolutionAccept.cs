using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestAlternativeSolutionAccept : ICommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly float DifficultyMultiplier;

    [ProtoMember(4)]
    public readonly TroopRosterElementData[] SentTroops;

    public RequestAlternativeSolutionAccept(
        string issueOwnerId,
        string issueId,
        float difficultyMultiplier,
        TroopRosterElementData[] sentTroops)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        DifficultyMultiplier = difficultyMultiplier;
        SentTroops = sentTroops;
    }
}
