using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Artisan Overpriced Goods issue replication - creation-time antagonist-merchant/
/// requested-trade-good capture only (same shape as <see cref="SmugglersIssueHandler"/>'s creation half, minus
/// any further bespoke mechanic).
///
/// Deliberately does NOT subscribe to any accept-quest/alternative-solution-accept/finalize message of its
/// own - this issue type rides <see cref="Patches.IssueAcceptancePatches"/>/
/// <see cref="Patches.NewIssueTypesAlternativeSolutionOwnershipGatePatch"/>/<see cref="Patches.IssueFinalizedPatches"/>
/// entirely unchanged (see <see cref="Interfaces.GenericAcceptMirrorIssueTypes"/>'s entries and
/// <see cref="Interfaces.IArtisanOverpricedGoodsIssueInterface"/>'s type doc comment for why nothing here
/// needs its own bespoke accept/finalize capture). The genuinely bespoke mechanic this issue type needs (the
/// <c>AntagonistHero</c>/<c>CounterOfferHero</c> identity fix) is a pure, local, network-message-free Harmony
/// patch - see <see cref="Patches.ArtisanOverpricedGoodsAntagonistIdentityPatches"/> - so it has no handler of
/// its own either.
/// </summary>
internal class ArtisanOverpricedGoodsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ArtisanOverpricedGoodsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IArtisanOverpricedGoodsIssueInterface issueInterface;

    public ArtisanOverpricedGoodsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IArtisanOverpricedGoodsIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<ArtisanOverpricedGoodsIssueCreated>(Handle_ArtisanOverpricedGoodsIssueCreated);
        messageBroker.Subscribe<NetworkArtisanOverpricedGoodsIssueCreated>(Handle_NetworkArtisanOverpricedGoodsIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ArtisanOverpricedGoodsIssueCreated>(Handle_ArtisanOverpricedGoodsIssueCreated);
        messageBroker.Unsubscribe<NetworkArtisanOverpricedGoodsIssueCreated>(Handle_NetworkArtisanOverpricedGoodsIssueCreated);
    }

    // --- Creation: server rolls once, broadcasts the resolved antagonist merchant/requested trade good ---

    private void Handle_ArtisanOverpricedGoodsIssueCreated(MessagePayload<ArtisanOverpricedGoodsIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var counterOfferHero, out var requestedTradeGood))
        {
            Logger.Error("Could not capture Artisan Overpriced Goods issue fields for owner {Owner}", ownerId);
            return;
        }

        // Defensive check (task item 3): CalculateTradeGoodsAmountAndReward() already ran inside the real ctor
        // - MathF.Max(..., 1) makes RequestedTradeGoodAmount == 0 structurally impossible, but nothing
        // equivalently floors _goldReward, and ArtisanOverpricedGoodsIssue.OnGameLoad silently re-derives BOTH
        // (live, from whatever THAT peer's own IssueDifficultyMultiplier is at load time) the instant either is
        // ever exactly 0 - logged loudly here rather than left to silently diverge across peers on a later
        // save/load.
        if (issue.RequestedTradeGoodAmount == 0 || issue.RewardGold == 0)
        {
            Logger.Error(
                "Artisan Overpriced Goods issue for owner {Owner} landed on RequestedTradeGoodAmount={Amount}/" +
                "RewardGold={Reward} right after creation - OnGameLoad's live-recompute guard will silently " +
                "re-derive whichever is 0 on every peer's own NEXT save/load using THAT peer's own live " +
                "IssueDifficultyMultiplier, a real cross-peer divergence risk",
                ownerId, issue.RequestedTradeGoodAmount, issue.RewardGold);
        }

        if (!objectManager.TryGetIdWithLogging(counterOfferHero, out var counterOfferHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(requestedTradeGood, out var requestedTradeGoodId)) return;

        network.SendAll(new NetworkArtisanOverpricedGoodsIssueCreated(ownerId, counterOfferHeroId, requestedTradeGoodId));
    }

    private void Handle_NetworkArtisanOverpricedGoodsIssueCreated(MessagePayload<NetworkArtisanOverpricedGoodsIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.CounterOfferHeroId, out var counterOfferHero)) return;
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.RequestedTradeGoodId, out var requestedTradeGood)) return;

            var replicated = issueInterface.ConstructReplicated(owner, counterOfferHero, requestedTradeGood);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }
}
