using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.ArtisanOverpricedGoodsIssueCreationPatch"/>) immediately
/// after <c>IssueManager.CreateNewIssue</c> creates a new
/// <see cref="ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue"/>. Carries the live issue so
/// <see cref="Handlers.ArtisanOverpricedGoodsIssueHandler"/> can capture the picked antagonist merchant/
/// requested trade good and replicate them. Accept-quest/alternative-solution-accept/finalize are NOT handled
/// here at all - this type rides the fully generic <see cref="Interfaces.GenericAcceptMirrorIssueTypes"/>
/// mechanism unchanged (see <see cref="Interfaces.IArtisanOverpricedGoodsIssueInterface"/>'s doc comment).
/// </summary>
public readonly struct ArtisanOverpricedGoodsIssueCreated : IEvent
{
    public readonly ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue Issue;

    public ArtisanOverpricedGoodsIssueCreated(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue issue)
    {
        Issue = issue;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: the authoritatively-picked antagonist merchant/requested trade good for a
/// newly created Artisan Overpriced Goods issue, so every client constructs a byte-identical instance instead
/// of independently re-rolling <c>GetAntagonistMerchant</c>'s <c>GetRandomElementWithPredicate</c> locally.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanOverpricedGoodsIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string CounterOfferHeroId;
    [ProtoMember(3)]
    public readonly string RequestedTradeGoodId;

    public NetworkArtisanOverpricedGoodsIssueCreated(string ownerId, string counterOfferHeroId, string requestedTradeGoodId)
    {
        OwnerId = ownerId;
        CounterOfferHeroId = counterOfferHeroId;
        RequestedTradeGoodId = requestedTradeGoodId;
    }
}
