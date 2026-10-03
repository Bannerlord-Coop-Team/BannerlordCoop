using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct ArtisanProductIssueCreated : IEvent
{
    public readonly ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue Issue;

    public ArtisanProductIssueCreated(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanProductIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string TargetSettlementId;
    [ProtoMember(3)]
    public readonly string TargetHeroId;
    [ProtoMember(4)]
    public readonly string ItemId;
    [ProtoMember(5)]
    public readonly string CounterOfferHeroId;
    [ProtoMember(6)]
    public readonly int Generation;
    [ProtoMember(7)]
    public readonly CampaignTime DueTime;
    [ProtoMember(8)]
    public readonly string IssueId;
    [ProtoMember(9)]
    public readonly int NextIssueIndex;

    public NetworkArtisanProductIssueCreated(string ownerId, string targetSettlementId, string targetHeroId,
        string itemId, string counterOfferHeroId, int generation, CampaignTime dueTime, string issueId, int nextIssueIndex)
    {
        OwnerId = ownerId;
        TargetSettlementId = targetSettlementId;
        TargetHeroId = targetHeroId;
        ItemId = itemId;
        CounterOfferHeroId = counterOfferHeroId;
        Generation = generation;
        DueTime = dueTime;
        IssueId = issueId;
        NextIssueIndex = nextIssueIndex;
    }
}
