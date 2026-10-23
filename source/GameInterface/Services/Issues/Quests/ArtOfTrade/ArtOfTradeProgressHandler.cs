using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtOfTrade;

internal sealed class ArtOfTradeProgressHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IControllerIdProvider controller;
    private readonly IIssueOwnerResolver owners;
    private readonly Dictionary<Hero, TroopRoster> returningTroops = new Dictionary<Hero, TroopRoster>();

    public ArtOfTradeProgressHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueResolver issues, IIssueOwnershipRegistry ownership, IPlayerManager players, IControllerIdProvider controller,
        IIssueOwnerResolver owners)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.issues = issues;
        this.ownership = ownership;
        this.players = players;
        this.controller = controller;
        this.owners = owners;
        broker.Subscribe<ArtOfTradeProgressChanged>(Progress);
        broker.Subscribe<RequestArtOfTradeProgress>(ReceiveProgress);
        broker.Subscribe<ArtOfTradeJournalFinalizing>(Finalizing);
        broker.Subscribe<NetworkArtOfTradeJournal>(ReceiveJournal);
        broker.Subscribe<ArtOfTradeIssueJournalChanged>(IssueJournal);
        broker.Subscribe<ArtOfTradePurchaseRejected>(RejectPurchase);
        broker.Subscribe<NetworkArtOfTradePurchaseRejected>(ReceivePurchaseRejected);
        broker.Subscribe<ArtOfTradeTroopsDeferred>(DeferTroops);
        broker.Subscribe<ArtOfTradeHourlyTick>(ReturnTroops);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArtOfTradeProgressChanged>(Progress);
        broker.Unsubscribe<RequestArtOfTradeProgress>(ReceiveProgress);
        broker.Unsubscribe<ArtOfTradeJournalFinalizing>(Finalizing);
        broker.Unsubscribe<NetworkArtOfTradeJournal>(ReceiveJournal);
        broker.Unsubscribe<ArtOfTradeIssueJournalChanged>(IssueJournal);
        broker.Unsubscribe<ArtOfTradePurchaseRejected>(RejectPurchase);
        broker.Unsubscribe<NetworkArtOfTradePurchaseRejected>(ReceivePurchaseRejected);
        broker.Unsubscribe<ArtOfTradeTroopsDeferred>(DeferTroops);
        broker.Unsubscribe<ArtOfTradeHourlyTick>(ReturnTroops);
    }

    private void DeferTroops(MessagePayload<ArtOfTradeTroopsDeferred> payload)
    {
        if (ModInformation.IsClient || !owners.TryResolveOwnerHero(payload.What.Issue, out var hero)) return;
        if (!returningTroops.TryGetValue(hero, out var roster))
        {
            roster = TroopRoster.CreateDummyTroopRoster();
            returningTroops.Add(hero, roster);
        }
        // Finalization clears the issue roster and ownership before a busy player can receive troops.
        roster.Add(payload.What.Issue.AlternativeSolutionSentTroops);
    }

    private void ReturnTroops(MessagePayload<ArtOfTradeHourlyTick> payload)
    {
        if (ModInformation.IsClient) return;
        foreach (var entry in returningTroops.ToArray())
        {
            if (entry.Key.PartyBelongedTo == null) continue;
            using var scope = new MainHeroSubstitutionScope(entry.Key, entry.Key.PartyBelongedTo);
            if (!Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) continue;
            Campaign.Current.IssueManager.MakeAlternativeTroopsReturn(entry.Value);
            returningTroops.Remove(entry.Key);
        }
    }

    private void Progress(MessagePayload<ArtOfTradeProgressChanged> payload)
    {
        var quest = payload.What.Quest;
        if (ModInformation.IsServer || !objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId)) return;
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null) return;
        network.SendAll(new RequestArtOfTradeProgress(giverId, issue.StringId, quest._soldCount, quest._gatheredDenars, quest._daysPassed));
    }

    private void ReceiveProgress(MessagePayload<RequestArtOfTradeProgress> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer) || !players.TryGetPlayer(peer, out var player)) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var owner) || owner != player.ControllerId ||
                !(issue.IssueQuest is Quest quest) || !quest.IsOngoing || quest._soldItemAmountLog == null) return;
            if (data.SoldCount < quest._soldCount || data.GatheredDenars < quest._gatheredDenars || data.DaysPassed < quest._daysPassed) return;
            quest._soldCount = data.SoldCount;
            quest._gatheredDenars = data.GatheredDenars;
            quest._daysPassed = data.DaysPassed;
            quest._productsUndersold = quest._gatheredDenars < quest._targetDenarsToAchieve;
            quest._soldItemAmountLog.UpdateCurrentProgress(quest._soldCount);
            quest._gatheredDenarsLog.UpdateCurrentProgress(quest._gatheredDenars);
        }, context: nameof(ArtOfTradeProgressHandler));
    }

    private void Finalizing(MessagePayload<ArtOfTradeJournalFinalizing> payload)
    {
        var quest = payload.What.Quest;
        if (ModInformation.IsClient || !objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId)) return;
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !ownership.TryGetOwner(giverId, issue.StringId, out var owner)) return;
        var entries = quest.JournalEntries.Skip(quest._soldItemAmountLog == null ? 0 : 5)
            .Select(log => new ArtOfTradeJournalEntry(log)).ToArray();
        // An ending without its own log has nothing new for the owner, and an empty array would arrive as null
        if (entries.Length == 0) return;
        network.SendAll(new NetworkArtOfTradeJournal(giverId, issue.StringId, owner, entries));
    }

    private void ReceiveJournal(MessagePayload<NetworkArtOfTradeJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (data.ControllerId != controller.ControllerId ||
                !issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _)) return;
            if (data.IsIssueJournal)
            {
                issue._journalEntries.Clear();
                foreach (var entry in data.Entries) issue._journalEntries.Add(entry.CreateLog());
                issue._areIssueEffectsResolved = data.EffectsResolved;
                var journal = Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>();
                if (data.Details == IssueBase.IssueUpdateDetails.None)
                    journal?.OnIssueLogAdded(issue, true);
                else
                    journal?.OnIssueUpdated(issue, data.Details, Hero.MainHero);
                return;
            }
            if (!(issue.IssueQuest is Quest quest)) return;
            foreach (var entry in data.Entries) quest._journalEntries.Add(entry.CreateLog());
            CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
        }, context: nameof(ArtOfTradeProgressHandler));
    }

    private void IssueJournal(MessagePayload<ArtOfTradeIssueJournalChanged> payload)
    {
        var issue = payload.What.Issue;
        if (ModInformation.IsClient || !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId) ||
            !ownership.TryGetOwner(giverId, issue.StringId, out var owner)) return;
        network.SendAll(new NetworkArtOfTradeJournal(giverId, issue.StringId, owner,
            issue.JournalEntries.Select(log => new ArtOfTradeJournalEntry(log)).ToArray(), true,
            payload.What.Details, issue._areIssueEffectsResolved));
    }

    private void RejectPurchase(MessagePayload<ArtOfTradePurchaseRejected> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = IssueManager.GetIssueOfQuest(payload.What.Quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId) ||
            !ownership.TryGetOwner(giverId, issue.StringId, out var owner)) return;
        RestoreOffer(issue);
        ownership.Remove(giverId);
        network.SendAll(new NetworkArtOfTradePurchaseRejected(giverId, issue.StringId, owner));
    }

    private void ReceivePurchaseRejected(MessagePayload<NetworkArtOfTradePurchaseRejected> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _)) return;
            RestoreOffer(issue);
            ownership.Remove(data.IssueOwnerId);
            if (data.ControllerId == controller.ControllerId)
                MBInformationManager.AddQuickInformation(new TextObject(
                    "{=coop_trade_purchase_refused}The goods could not be purchased. Speak to the merchant again to check your gold and choose how to trade."));
        }, context: nameof(ArtOfTradeProgressHandler));
    }

    private static void RestoreOffer(IssueBase issue)
    {
        var quest = issue.IssueQuest;
        // Unlink first so canceling the provisional quest cannot finalize the restored issue.
        issue.IssueQuest = null;
        quest?.CompleteQuestWithCancel();
        issue._issueState = IssueBase.IssueState.Ongoing;
        issue.IsTriedToSolveBefore = false;
        issue.IssueDueTime = issue.IssueCreationTime + CampaignTime.Days(30f);
    }
}
