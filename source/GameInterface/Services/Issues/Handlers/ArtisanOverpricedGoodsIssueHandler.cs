using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using SandBox.CampaignBehaviors;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

internal sealed class ArtisanOverpricedGoodsIssueHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IIssueGenerationRegistry generationRegistry;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IArtisanOverpricedGoodsActions actions;
    private readonly IArtisanOverpricedGoodsIssueInterface issueInterface;
    private readonly CreationCaptureRunner<Issue,
        (ItemObject Item, Hero CounterOfferHero, ArtisanOverpricedGoodsIssueValues Values)> creation;

    public ArtisanOverpricedGoodsIssueHandler(IMessageBroker messageBroker, IObjectManager objectManager,
        INetwork network, IIssueGenerationRegistry generationRegistry, IArtisanOverpricedGoodsIssueInterface issueInterface,
        IIssueOwnershipRegistry ownership, IPlayerManager players, IArtisanOverpricedGoodsActions actions)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.generationRegistry = generationRegistry;
        this.ownership = ownership;
        this.players = players;
        this.actions = actions;
        this.issueInterface = issueInterface;
        creation = new(issueInterface, IssueBase.IssueFrequency.Common);
        messageBroker.Subscribe<ArtisanOverpricedGoodsIssueCreated>(HandleCreated);
        messageBroker.Subscribe<NetworkArtisanOverpricedGoodsIssueCreated>(HandleNetworkCreated);
        messageBroker.Subscribe<RequestArtisanOverpricedGoodsAction>(HandleActionRequest);
        messageBroker.Subscribe<NetworkArtisanOverpricedGoodsActionApplied>(HandleActionApplied);
        messageBroker.Subscribe<ArtisanIssueOutcome>(HandleOutcome);
        messageBroker.Subscribe<NetworkArtisanIssueOutcome>(HandleNetworkOutcome);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ArtisanOverpricedGoodsIssueCreated>(HandleCreated);
        messageBroker.Unsubscribe<NetworkArtisanOverpricedGoodsIssueCreated>(HandleNetworkCreated);
        messageBroker.Unsubscribe<RequestArtisanOverpricedGoodsAction>(HandleActionRequest);
        messageBroker.Unsubscribe<NetworkArtisanOverpricedGoodsActionApplied>(HandleActionApplied);
        messageBroker.Unsubscribe<ArtisanIssueOutcome>(HandleOutcome);
        messageBroker.Unsubscribe<NetworkArtisanIssueOutcome>(HandleNetworkOutcome);
    }

    private void HandleOutcome(MessagePayload<ArtisanIssueOutcome> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) ||
            !generationRegistry.TryGetGeneration(issue.IssueOwner, out var generation) ||
            !ownership.TryGetOwnerControllerId(issue.IssueOwner, out var controller)) return;
        var entries = payload.What.QuestJournal ? issue.IssueQuest.JournalEntries : issue.JournalEntries;
        network.SendAll(new NetworkArtisanIssueOutcome(ownerId, generation, controller, payload.What.Details,
            entries.Select(log => new ArtisanJournalEntry(log)).ToArray(), issue._areIssueEffectsResolved, payload.What.QuestJournal));
    }

    private void HandleNetworkOutcome(MessagePayload<NetworkArtisanIssueOutcome> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var giver) || giver.Issue is not Issue issue) return;
            if (!generationRegistry.TryGetGeneration(giver, out var generation) || generation != data.Generation ||
                !ownership.TryGetOwnerControllerId(giver, out var controller) || controller != data.ControllerId) return;
            if (!ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) ||
                !context.TryEnter(giver, out var ownerScope)) return;
            using (ownerScope)
            using (new AllowedThread())
            {
                if (data.QuestJournal)
                {
                    if (issue.IssueQuest is not Quest quest) return;
                    quest._journalEntries.Clear();
                    foreach (var entry in data.Entries) quest._journalEntries.Add(entry.ToLog());
                    return;
                }
                issue._journalEntries.Clear();
                foreach (var entry in data.Entries) issue._journalEntries.Add(entry.ToLog());
                issue._areIssueEffectsResolved = data.EffectsResolved;
                Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>()?.OnIssueUpdated(issue, data.Details, Hero.MainHero);
                Campaign.Current.GetCampaignBehavior<DefaultNotificationsCampaignBehavior>()?.OnIssueUpdated(issue, data.Details, Hero.MainHero);
            }
        });
    }

    private void HandleCreated(MessagePayload<ArtisanOverpricedGoodsIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!creation.TryCapture(issue, out var fields)) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(fields.Item, out var itemId)) return;
        if (!objectManager.TryGetIdWithLogging(fields.CounterOfferHero, out var counterOfferHeroId)) return;

        network.SendAll(new NetworkArtisanOverpricedGoodsIssueCreated(ownerId, itemId, counterOfferHeroId,
            generationRegistry.Bump(issue.IssueOwner), fields.Values));
    }

    private void HandleNetworkCreated(MessagePayload<NetworkArtisanOverpricedGoodsIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (generationRegistry.TryGetGeneration(owner, out var current) && data.Generation <= current) return;
            if (owner.Issue != null) return;
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.ItemId, out var item)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.CounterOfferHeroId, out var counterOfferHero)) return;

            creation.ConstructAndRegisterReplicated(owner, (item, counterOfferHero, data.Values),
                (issue, fields) => issueInterface.ApplyValues(issue, fields.Values));
            generationRegistry.SetGeneration(owner, data.Generation);
        });
    }

    private void HandleActionRequest(MessagePayload<RequestArtisanOverpricedGoodsAction> payload)
    {
        if (ModInformation.IsClient) return;
        var requester = payload.Who as NetPeer;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (requester == null || !players.TryGetPlayer(requester, out var player)) return;
            actions.Apply(player, data);
        });
    }

    private void HandleActionApplied(MessagePayload<NetworkArtisanOverpricedGoodsActionApplied> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!generationRegistry.TryGetGeneration(owner, out var generation) || generation != data.Generation) return;
            if (owner.Issue is not Issue issue) return;
            using (new AllowedThread())
            {
                if (data.Action == ArtisanOverpricedGoodsAction.StartLordSolution)
                {
                    if (!issue.IsOngoingWithoutQuest) return;
                    ownership.SetOwner(owner, data.ControllerId);
                    if (!ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) ||
                        !context.TryEnter(owner, out var scope)) return;
                    using (scope)
                    {
                        issueInterface.ApplyValues(issue, data.Values);
                        issue.StartIssueWithLordSolution();
                    }
                    return;
                }
                if (!ownership.TryGetOwnerControllerId(owner, out var controller) || controller != data.ControllerId) return;
                if (issue.IssueQuest is Quest quest)
                {
                    if (data.Action == ArtisanOverpricedGoodsAction.DeliverPartial || data.Action == ArtisanOverpricedGoodsAction.DeliverFull)
                    {
                        quest._givenTradeGoods = data.Delivered;
                        quest.UpdateQuestTaskStage(quest._playerStartsQuestLog, data.Delivered);
                    }
                }
            }
        });
    }
}
