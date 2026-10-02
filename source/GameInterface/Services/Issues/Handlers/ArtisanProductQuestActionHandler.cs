using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Handlers;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

internal sealed class ArtisanProductQuestActionHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IIssueGenerationRegistry generations;
    private readonly IArtisanProductQuestActions actions;

    public ArtisanProductQuestActionHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IPlayerManager players, IIssueOwnershipRegistry ownership, IIssueGenerationRegistry generations,
        IArtisanProductQuestActions actions)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.players = players;
        this.ownership = ownership;
        this.generations = generations;
        this.actions = actions;
        broker.Subscribe<ArtisanProductQuestActionRequested>(HandleRequested);
        broker.Subscribe<RequestArtisanProductQuestAction>(HandleRequest);
        broker.Subscribe<NetworkArtisanProductQuestProgress>(HandleProgress);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArtisanProductQuestActionRequested>(HandleRequested);
        broker.Unsubscribe<RequestArtisanProductQuestAction>(HandleRequest);
        broker.Unsubscribe<NetworkArtisanProductQuestProgress>(HandleProgress);
    }

    private void HandleRequested(MessagePayload<ArtisanProductQuestActionRequested> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        if (!ownership.IsLocalPeerOwner(data.Giver)) return;
        if (!generations.TryGetGeneration(data.Giver, out var generation)) return;
        if (!objects.TryGetIdWithLogging(data.Giver, out var giverId)) return;
        network.SendAll(new RequestArtisanProductQuestAction(giverId, generation, data.Action, data.ExpectedDelivered));
    }

    private void HandleRequest(MessagePayload<RequestArtisanProductQuestAction> payload)
    {
        if (ModInformation.IsClient) return;
        var data = payload.What;
        var peer = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (peer == null || !players.TryGetPlayer(peer, out var player)) return;
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (!ownership.TryGetOwnerControllerId(giver, out var owner) || owner != player.ControllerId) return;
            if (!generations.TryGetGeneration(giver, out var generation) || generation != data.Generation) return;
            if (giver.Issue is not Issue issue || issue.IssueQuest is not Quest quest) return;
            if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return;
            if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;
            if (!actions.TryApply(issue, hero, party, data.Action, data.ExpectedDelivered)) return;

            if (quest.IsOngoing)
                network.SendAll(new NetworkArtisanProductQuestProgress(data.GiverId, generation,
                    quest._deliveredRawGoods, quest._counterOfferRefused, quest._counterOfferGiven));
        });
    }

    private void HandleProgress(MessagePayload<NetworkArtisanProductQuestProgress> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (!generations.TryGetGeneration(giver, out var generation) || generation != data.Generation) return;
            if (giver.Issue?.IssueQuest is not Quest quest || !quest.IsOngoing) return;
            if (data.Delivered < quest._deliveredRawGoods || data.Delivered > quest._amountOfRawGoodsToBeDelivered) return;
            using (new AllowedThread())
            {
                quest._deliveredRawGoods = data.Delivered;
                quest._counterOfferRefused |= data.MerchantRefused;
                quest._counterOfferGiven |= data.MerchantOfferGiven;
                if (quest._playerStartsQuestLog != null)
                    quest.UpdateQuestTaskStage(quest._playerStartsQuestLog, data.Delivered);
            }
        });
    }
}
