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

internal sealed class ArtisanProductLordActionHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IIssueGenerationRegistry generations;
    private readonly IIssueConversationTracker conversations;
    private readonly IArtisanProductLordActions actions;

    public ArtisanProductLordActionHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IPlayerManager players, IIssueOwnershipRegistry ownership, IIssueGenerationRegistry generations,
        IIssueConversationTracker conversations, IArtisanProductLordActions actions)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.players = players;
        this.ownership = ownership;
        this.generations = generations;
        this.conversations = conversations;
        this.actions = actions;
        broker.Subscribe<ArtisanProductLordActionRequested>(HandleRequested);
        broker.Subscribe<RequestArtisanProductLordAction>(HandleRequest);
        broker.Subscribe<NetworkArtisanProductLordStarted>(HandleStarted);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArtisanProductLordActionRequested>(HandleRequested);
        broker.Unsubscribe<RequestArtisanProductLordAction>(HandleRequest);
        broker.Unsubscribe<NetworkArtisanProductLordStarted>(HandleStarted);
    }

    private void HandleRequested(MessagePayload<ArtisanProductLordActionRequested> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        if (data.Action != ArtisanProductLordAction.Start && !ownership.IsLocalPeerOwner(data.Giver)) return;
        if (!generations.TryGetGeneration(data.Giver, out var generation)) return;
        if (!objects.TryGetIdWithLogging(data.Giver, out var giverId)) return;
        network.SendAll(new RequestArtisanProductLordAction(giverId, generation, data.Action));
    }

    private void HandleRequest(MessagePayload<RequestArtisanProductLordAction> payload)
    {
        if (ModInformation.IsClient) return;
        var data = payload.What;
        var peer = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (peer == null || !players.TryGetPlayer(peer, out var player)) return;
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (!generations.TryGetGeneration(giver, out var generation) || generation != data.Generation) return;
            if (giver.Issue is not Issue issue) return;
            if (data.Action == ArtisanProductLordAction.Start)
            {
                if (ownership.TryGetOwnerControllerId(giver, out _)) return;
                if (!conversations.TryGetTrackedRequester(data.GiverId, player.ControllerId, out var tracked) || tracked != generation) return;
            }
            else if (!ownership.TryGetOwnerControllerId(giver, out var owner) || owner != player.ControllerId) return;

            if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return;
            if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;
            if (!actions.TryApply(issue, hero, party, data.Action)) return;
            if (data.Action != ArtisanProductLordAction.Start) return;

            ownership.SetOwner(giver, player.ControllerId);
            network.SendAll(new NetworkArtisanProductLordStarted(data.GiverId, generation,
                player.ControllerId, issue.IssueDifficultyMultiplier, CampaignTime.Now));
        });
    }

    private void HandleStarted(MessagePayload<NetworkArtisanProductLordStarted> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (!generations.TryGetGeneration(giver, out var generation) || generation != data.Generation) return;
            if (giver.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
            using (new AllowedThread())
            {
                issue._issueDifficultyMultiplier = data.Difficulty;
                issue._issueState = IssueBase.IssueState.SolvingWithLordSolution;
                issue.IsTriedToSolveBefore = true;
                issue.AddLog(new JournalLog(data.StartedAt, issue.LordSolutionStartLog));
                ownership.SetOwner(giver, data.ControllerId);
                CampaignEvents.BeforeGameMenuOpenedEvent.AddNonSerializedListener(issue, issue.BeforeGameMenuOpened);
            }
        });
    }
}
