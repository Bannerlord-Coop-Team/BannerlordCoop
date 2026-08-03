using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

// --- Creation: server rolls once (three chained rolls), broadcasts the resolved terms. Acceptance/accept-race/
// ownership/finalize all ride the fully generic VillageNeedsToolsIssueHandler/VillageNeedsToolsIssueInterface
// machinery unchanged (see IHeadmanNeedsToDeliverAHerdIssueInterface's doc comment) - no bespoke messages
// needed for those. ---

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates a
/// <see cref="HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue"/>, carrying the live
/// issue so <see cref="Handlers.HeadmanNeedsToDeliverAHerdIssueHandler"/> can capture its three chained
/// creation-time-rolled fields.</summary>
public readonly struct HeadmanNeedsToDeliverAHerdIssueCreated : IEvent
{
    public readonly HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue Issue;

    public HeadmanNeedsToDeliverAHerdIssueCreated(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanNeedsToDeliverAHerdIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string TargetSettlementId;
    [ProtoMember(3)]
    public readonly string TargetHeroId;
    [ProtoMember(4)]
    public readonly string HerdTypeToDeliverId;

    public NetworkHeadmanNeedsToDeliverAHerdIssueCreated(
        string ownerId, string targetSettlementId, string targetHeroId, string herdTypeToDeliverId)
    {
        OwnerId = ownerId;
        TargetSettlementId = targetSettlementId;
        TargetHeroId = targetHeroId;
        HerdTypeToDeliverId = herdTypeToDeliverId;
    }
}
