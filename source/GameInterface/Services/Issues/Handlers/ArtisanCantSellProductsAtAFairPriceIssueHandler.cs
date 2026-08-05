using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative CREATION handling for Artisan Can't Sell Products At A Fair Price (see
/// <see cref="IArtisanCantSellProductsAtAFairPriceIssueInterface"/>'s doc comment for why this is the only
/// bespoke piece this type needs). Acceptance (quest AND alternative solution), accept-race arbitration, real
/// ownership recording, and finalize/removal all ride the fully generic <see cref="VillageNeedsToolsIssueHandler"/>
/// + <see cref="VillageNeedsToolsIssueInterface"/> + <see cref="VillageNeedsToolsIssueOwnership"/> machinery
/// unchanged - this type is registered into <see cref="GenericAcceptMirrorIssueTypes"/>'s two eligible sets
/// instead of growing its own parallel accept/reject message set.
/// </summary>
internal class ArtisanCantSellProductsAtAFairPriceIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ArtisanCantSellProductsAtAFairPriceIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IArtisanCantSellProductsAtAFairPriceIssueInterface issueInterface;

    public ArtisanCantSellProductsAtAFairPriceIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IArtisanCantSellProductsAtAFairPriceIssueInterface issueInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;

        messageBroker.Subscribe<ArtisanCantSellProductsAtAFairPriceIssueCreated>(Handle_ArtisanCantSellProductsAtAFairPriceIssueCreated);
        messageBroker.Subscribe<NetworkArtisanCantSellProductsAtAFairPriceIssueCreated>(Handle_NetworkArtisanCantSellProductsAtAFairPriceIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ArtisanCantSellProductsAtAFairPriceIssueCreated>(Handle_ArtisanCantSellProductsAtAFairPriceIssueCreated);
        messageBroker.Unsubscribe<NetworkArtisanCantSellProductsAtAFairPriceIssueCreated>(Handle_NetworkArtisanCantSellProductsAtAFairPriceIssueCreated);
    }

    private void Handle_ArtisanCantSellProductsAtAFairPriceIssueCreated(MessagePayload<ArtisanCantSellProductsAtAFairPriceIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var targetSettlement, out var targetHero, out var rawMaterialsToBeDelivered, out var counterOfferHero))
        {
            Logger.Error("Could not capture Artisan Can't Sell Products At A Fair Price issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(targetSettlement, out var targetSettlementId)) return;
        if (!objectManager.TryGetIdWithLogging(targetHero, out var targetHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(rawMaterialsToBeDelivered, out var rawMaterialsToBeDeliveredId)) return;
        if (!objectManager.TryGetIdWithLogging(counterOfferHero, out var counterOfferHeroId)) return;

        network.SendAll(new NetworkArtisanCantSellProductsAtAFairPriceIssueCreated(
            ownerId, targetSettlementId, targetHeroId, rawMaterialsToBeDeliveredId, counterOfferHeroId));
    }

    private void Handle_NetworkArtisanCantSellProductsAtAFairPriceIssueCreated(MessagePayload<NetworkArtisanCantSellProductsAtAFairPriceIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.TargetSettlementId, out var targetSettlement)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.TargetHeroId, out var targetHero)) return;
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.RawMaterialsToBeDeliveredId, out var rawMaterialsToBeDelivered)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.CounterOfferHeroId, out var counterOfferHero)) return;

            var replicated = issueInterface.ConstructReplicated(owner, targetSettlement, targetHero, rawMaterialsToBeDelivered, counterOfferHero);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }
}
