using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Handlers;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

internal sealed class ArtisanProductJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueGenerationRegistry generations;
    private readonly IArtisanProductJournal journal;

    public ArtisanProductJournalHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueGenerationRegistry generations, IArtisanProductJournal journal)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.generations = generations;
        this.journal = journal;
        broker.Subscribe<ArtisanProductJournalChanged>(HandleChanged);
        broker.Subscribe<NetworkArtisanProductJournal>(HandleJournal);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArtisanProductJournalChanged>(HandleChanged);
        broker.Unsubscribe<NetworkArtisanProductJournal>(HandleJournal);
    }

    private void HandleChanged(MessagePayload<ArtisanProductJournalChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        if (!generations.TryGetGeneration(issue.IssueOwner, out var generation)) return;
        var quest = issue.IssueQuest as Quest;
        var history = Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>()?.GetRelatedLog(issue);
        // This precedes NetworkIssueRemoved on the same reliable ordered channel.
        network.SendAll(new NetworkArtisanProductJournal(giverId, generation, journal.Pack(issue.JournalEntries),
            quest == null ? Array.Empty<ArtisanProductLogEntry>() : journal.Pack(quest.JournalEntries),
            quest == null ? -1 : quest._journalEntries.IndexOf(quest._playerStartsQuestLog),
            quest?._deliveredRawGoods ?? 0, quest?._counterOfferRefused ?? false,
            history?._lastIssueStatus ?? IssueBase.IssueUpdateDetails.None,
            history?._questCompletionDetail ?? QuestBase.QuestCompleteDetails.Invalid,
            quest?._counterOfferGiven ?? false, issue._areIssueEffectsResolved));
    }

    private void HandleJournal(MessagePayload<NetworkArtisanProductJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (!generations.TryGetGeneration(giver, out var generation) || generation != data.Generation) return;
            if (giver.Issue is not Issue issue) return;
            var previousIssue = issue.JournalEntries.ToArray();
            var previousQuest = issue.IssueQuest?.JournalEntries.ToArray() ?? Array.Empty<JournalLog>();
            var issueEntries = journal.Merge(previousIssue, data.IssueEntries);
            var questEntries = journal.Merge(previousQuest, data.QuestEntries);
            using (new AllowedThread())
            {
                issue._journalEntries.Clear();
                issue._journalEntries.AddRange(issueEntries);
                issue._areIssueEffectsResolved = data.IssueEffectsResolved;
                if (issue.IssueQuest is Quest quest)
                {
                    quest._journalEntries.Clear();
                    quest._journalEntries.AddRange(questEntries);
                    quest._playerStartsQuestLog = data.DeliveryLogIndex >= 0 && data.DeliveryLogIndex < questEntries.Length
                        ? questEntries[data.DeliveryLogIndex] : null;
                    quest._deliveredRawGoods = data.Delivered;
                    quest._counterOfferRefused = data.MerchantRefused;
                    quest._counterOfferGiven = data.MerchantOfferGiven;
                }
                if (issueEntries.Length == 0 && questEntries.Length == 0) return;
                var journals = Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>();
                if (journals == null) return;
                journals.OnIssueLogAdded(issue, true);
                var history = journals.GetRelatedLog(issue);
                history.Update(journals.GetEntries(issue), data.IssueStatus);
                if (data.QuestStatus != QuestBase.QuestCompleteDetails.Invalid)
                    history.Update(journals.GetEntries(issue), data.QuestStatus);
                var tracker = Campaign.Current.GetCampaignBehavior<ViewDataTrackerCampaignBehavior>();
                if (tracker != null)
                {
                    UpdateUnread(tracker._unExaminedQuestLogs, previousIssue, issueEntries);
                    UpdateUnread(tracker._unExaminedQuestLogs, previousQuest, questEntries);
                    tracker._isUnExaminedQuestLogJournalEntriesDirty = true;
                }
            }
        });
    }

    internal static void UpdateUnread(List<JournalLog> unread, JournalLog[] previous, JournalLog[] current)
    {
        for (var i = 0; i < previous.Length; i++)
        {
            var index = unread.IndexOf(previous[i]);
            if (index < 0) continue;
            if (i < current.Length) unread[index] = current[i];
            else unread.RemoveAt(index);
        }
        unread.AddRange(current.Skip(previous.Length));
    }
}
