using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct HeadmanNeedsToDeliverAHerdIssueCreated : IEvent
{
    public readonly HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue Issue;

    public HeadmanNeedsToDeliverAHerdIssueCreated(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanNeedsToDeliverAHerdIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string TargetSettlementId;
    [ProtoMember(3)]
    public readonly string TargetHeroId;
    [ProtoMember(4)]
    public readonly string HerdTypeToDeliverId;

    [ProtoMember(5)]
    public readonly int Generation;

    [ProtoMember(6)]
    public readonly string IssueId;
    [ProtoMember(7)]
    public readonly long CreationTimeTicks;
    [ProtoMember(8)]
    public readonly long DueTimeTicks;

    public NetworkHeadmanNeedsToDeliverAHerdIssueCreated(
        string ownerId, string targetSettlementId, string targetHeroId, string herdTypeToDeliverId, int generation,
        string issueId, long creationTimeTicks, long dueTimeTicks)
    {
        OwnerId = ownerId;
        TargetSettlementId = targetSettlementId;
        TargetHeroId = targetHeroId;
        HerdTypeToDeliverId = herdTypeToDeliverId;
        Generation = generation;
        IssueId = issueId;
        CreationTimeTicks = creationTimeTicks;
        DueTimeTicks = dueTimeTicks;
    }
}
