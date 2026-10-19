using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Messages;

/// <summary>
/// Sent by the server so every client offers the issue with the server's new targets.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkScoutEnemyGarrisonsTargetsChanged : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly byte[] Targets;

    public NetworkScoutEnemyGarrisonsTargetsChanged(string issueOwnerId, string issueId, byte[] targets)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Targets = targets;
    }
}
