using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

// --- Creation: server rolls once (four independent rolls), broadcasts the resolved terms. Acceptance/accept-
// race/ownership/finalize all ride the fully generic VillageNeedsToolsIssueHandler/VillageNeedsToolsIssueInterface
// machinery unchanged (see IArtisanCantSellProductsAtAFairPriceIssueInterface's doc comment) - no bespoke
// messages needed for those. ---

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates an
/// <see cref="ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue"/>,
/// carrying the live issue so <see cref="Handlers.ArtisanCantSellProductsAtAFairPriceIssueHandler"/> can capture
/// its four creation-time-rolled fields.</summary>
public readonly struct ArtisanCantSellProductsAtAFairPriceIssueCreated : IEvent
{
    public readonly ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue Issue;

    public ArtisanCantSellProductsAtAFairPriceIssueCreated(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -> all clients: the four server-picked creation-time fields (second-location target
/// settlement/hero, the raw materials item to be delivered, and the counter-offer hero) for a newly created
/// Artisan Can't Sell Products At A Fair Price issue, so every peer constructs a byte-identical mirror instead
/// of independently re-rolling any of them.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanCantSellProductsAtAFairPriceIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string TargetSettlementId;
    [ProtoMember(3)]
    public readonly string TargetHeroId;
    [ProtoMember(4)]
    public readonly string RawMaterialsToBeDeliveredId;
    [ProtoMember(5)]
    public readonly string CounterOfferHeroId;

    public NetworkArtisanCantSellProductsAtAFairPriceIssueCreated(
        string ownerId, string targetSettlementId, string targetHeroId, string rawMaterialsToBeDeliveredId, string counterOfferHeroId)
    {
        OwnerId = ownerId;
        TargetSettlementId = targetSettlementId;
        TargetHeroId = targetHeroId;
        RawMaterialsToBeDeliveredId = rawMaterialsToBeDeliveredId;
        CounterOfferHeroId = counterOfferHeroId;
    }
}
