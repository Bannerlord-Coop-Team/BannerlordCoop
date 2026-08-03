using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates a
/// <see cref="ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue"/>.</summary>
public readonly struct ScoutEnemyGarrisonsIssueCreated : IEvent
{
    public readonly ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue Issue;

    public ScoutEnemyGarrisonsIssueCreated(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -&gt; all clients: the three picked target settlements for a newly created Scout Enemy
/// Garrisons issue, so every client replicates the exact same targets instead of independently re-searching.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkScoutEnemyGarrisonsIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string Settlement1Id;
    [ProtoMember(3)]
    public readonly string Settlement2Id;
    [ProtoMember(4)]
    public readonly string Settlement3Id;

    public NetworkScoutEnemyGarrisonsIssueCreated(string ownerId, string settlement1Id, string settlement2Id, string settlement3Id)
    {
        OwnerId = ownerId;
        Settlement1Id = settlement1Id;
        Settlement2Id = settlement2Id;
        Settlement3Id = settlement3Id;
    }
}
