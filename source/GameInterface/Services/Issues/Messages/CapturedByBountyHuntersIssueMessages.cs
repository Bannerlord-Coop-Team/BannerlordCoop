using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct CapturedByBountyHuntersIssueCreated : IEvent
{
    public readonly CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue Issue;

    public CapturedByBountyHuntersIssueCreated(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCapturedByBountyHuntersIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string HideoutId;
    [ProtoMember(3)]
    public readonly int Generation;
    [ProtoMember(4)]
    public readonly float Difficulty;
    [ProtoMember(5)]
    public readonly string IssueId;
    [ProtoMember(6)]
    public readonly CampaignTime DueTime;

    public NetworkCapturedByBountyHuntersIssueCreated(string ownerId, string hideoutId, int generation, float difficulty, string issueId, CampaignTime dueTime)
    {
        OwnerId = ownerId;
        HideoutId = hideoutId;
        Generation = generation;
        Difficulty = difficulty;
        IssueId = issueId;
        DueTime = dueTime;
    }
}
