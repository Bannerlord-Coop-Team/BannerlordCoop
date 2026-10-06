using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Handlers;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

internal sealed class ArtisanProductIssueOwnerHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueGenerationRegistry generations;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IIssueConversationTracker conversations;

    public ArtisanProductIssueOwnerHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueGenerationRegistry generations, IIssueOwnershipRegistry ownership, IIssueConversationTracker conversations)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.generations = generations;
        this.ownership = ownership;
        this.conversations = conversations;
        broker.Subscribe<ArtisanProductIssueOwnerChanged>(HandleChanged);
        broker.Subscribe<NetworkArtisanProductIssueOwnerChanged>(HandleNetworkChanged);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArtisanProductIssueOwnerChanged>(HandleChanged);
        broker.Unsubscribe<NetworkArtisanProductIssueOwnerChanged>(HandleNetworkChanged);
    }

    private void HandleChanged(MessagePayload<ArtisanProductIssueOwnerChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var previous = payload.What.PreviousOwner;
        var owner = payload.What.Issue.IssueOwner;
        if (!objects.TryGetIdWithLogging(previous, out var previousId)) return;
        if (!objects.TryGetIdWithLogging(owner, out var ownerId)) return;
        if (!generations.TryGetGeneration(previous, out var previousGeneration)) return;
        ownership.TryGetOwnerControllerId(previous, out var controllerId);
        var generation = generations.Bump(owner);
        MoveOwnership(previous, owner, previousId, ownerId, controllerId);
        network.SendAll(new NetworkArtisanProductIssueOwnerChanged(previousId, previousGeneration,
            ownerId, generation, controllerId));
        broker.Publish(payload.What.Issue, new ArtisanProductJournalChanged(payload.What.Issue));
    }

    private void HandleNetworkChanged(MessagePayload<NetworkArtisanProductIssueOwnerChanged> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.PreviousOwnerId, out var previous)) return;
            if (!objects.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!generations.TryGetGeneration(previous, out var previousGeneration) ||
                previousGeneration != data.PreviousGeneration) return;
            if (generations.TryGetGeneration(owner, out var generation) && data.Generation <= generation) return;
            if (previous.Issue is not Issue issue || owner.Issue != null) return;
            using (new AllowedThread())
            {
                Campaign.Current.IssueManager.ChangeIssueOwner(issue, owner);
                generations.SetGeneration(owner, data.Generation);
                MoveOwnership(previous, owner, data.PreviousOwnerId, data.OwnerId, data.ControllerId);
            }
        });
    }

    private void MoveOwnership(Hero previous, Hero owner, string previousId, string ownerId, string controllerId)
    {
        ownership.Clear(previous);
        ownership.Clear(owner);
        ownership.SetOwner(owner, controllerId);
        conversations.Clear(previousId);
        conversations.Clear(ownerId);
    }
}
