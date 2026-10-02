using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

internal sealed class ArtisanProductIssueCreationHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueGenerationRegistry generations;
    private readonly IArtisanProductIssueCreation creation;

    public ArtisanProductIssueCreationHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueGenerationRegistry generations, IArtisanProductIssueCreation creation)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.generations = generations;
        this.creation = creation;
        broker.Subscribe<ArtisanProductIssueCreated>(HandleCreated);
        broker.Subscribe<NetworkArtisanProductIssueCreated>(HandleNetworkCreated);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArtisanProductIssueCreated>(HandleCreated);
        broker.Unsubscribe<NetworkArtisanProductIssueCreated>(HandleNetworkCreated);
    }

    private void HandleCreated(MessagePayload<ArtisanProductIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!creation.TryCapture(issue, out var fields)) return;
        if (!objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        if (!objects.TryGetIdWithLogging(fields.TargetSettlement, out var settlementId)) return;
        if (!objects.TryGetIdWithLogging(fields.TargetHero, out var targetId)) return;
        if (!objects.TryGetIdWithLogging(fields.Item, out var itemId)) return;
        if (!objects.TryGetIdWithLogging(fields.CounterOfferHero, out var merchantId)) return;

        network.SendAll(new NetworkArtisanProductIssueCreated(ownerId, settlementId, targetId,
            itemId, merchantId, generations.Bump(issue.IssueOwner), fields.DueTime, fields.IssueId, fields.NextIssueIndex));
    }

    private void HandleNetworkCreated(MessagePayload<NetworkArtisanProductIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (string.IsNullOrEmpty(data.IssueId) || data.NextIssueIndex <= 0) return;
            if (!objects.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (generations.TryGetGeneration(owner, out var current) && data.Generation <= current) return;
            if (owner.Issue != null) return;
            if (!objects.TryGetObjectWithLogging<Settlement>(data.TargetSettlementId, out var destination)) return;
            if (!objects.TryGetObjectWithLogging<Hero>(data.TargetHeroId, out var target)) return;
            if (!objects.TryGetObjectWithLogging<ItemObject>(data.ItemId, out var item)) return;
            if (!objects.TryGetObjectWithLogging<Hero>(data.CounterOfferHeroId, out var merchant)) return;

            creation.Apply(owner, new ArtisanProductIssueFields(destination, target, item, merchant,
                data.DueTime, data.IssueId, data.NextIssueIndex));
            generations.SetGeneration(owner, data.Generation);
        });
    }
}
