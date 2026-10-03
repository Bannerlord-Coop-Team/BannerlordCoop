using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

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
        broker.Subscribe<NetworkArtisanProductActionRejected>(HandleRejected);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArtisanProductQuestActionRequested>(HandleRequested);
        broker.Unsubscribe<RequestArtisanProductQuestAction>(HandleRequest);
        broker.Unsubscribe<NetworkArtisanProductQuestProgress>(HandleProgress);
        broker.Unsubscribe<NetworkArtisanProductActionRejected>(HandleRejected);
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
            if (!TryApply(data, player))
                network.Send(peer, new NetworkArtisanProductActionRejected(data.GiverId, data.Generation, false));
        });
    }

    private bool TryApply(RequestArtisanProductQuestAction data, Player player)
    {
        if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return false;
        if (!ownership.TryGetOwnerControllerId(giver, out var owner) || owner != player.ControllerId) return false;
        if (!generations.TryGetGeneration(giver, out var generation) || generation != data.Generation) return false;
        if (giver.Issue is not Issue issue || issue.IssueQuest is not Quest quest) return false;
        if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return false;
        if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;
        if (!actions.TryApply(issue, hero, party, data.Action, data.ExpectedDelivered)) return false;

        if (quest.IsOngoing)
            network.SendAll(new NetworkArtisanProductQuestProgress(data.GiverId, generation,
                quest._deliveredRawGoods, quest._counterOfferRefused, quest._counterOfferGiven));
        return true;
    }

    private void HandleRejected(MessagePayload<NetworkArtisanProductActionRejected> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (!generations.TryGetGeneration(giver, out var generation) || generation != data.Generation) return;
            if (data.LordStart) ArtisanProductQuestAcceptance.ResumeAcceptanceDialog(giver, accepted: false);
            MBInformationManager.AddQuickInformation(new TextObject("{=coop_artisan_choice_changed}This quest has changed and your choice could not be applied. Check your journal before trying again."));
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
