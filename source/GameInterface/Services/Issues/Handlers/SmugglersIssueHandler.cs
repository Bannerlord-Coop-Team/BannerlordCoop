using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Handlers;

internal sealed class SmugglersIssueHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IIssueGenerationRegistry generationRegistry;
    private readonly IPlayerManager players;
    private readonly IAwaitingAlternativeSolutionTroopsRegistry returningTroops;

    public SmugglersIssueHandler(IMessageBroker messageBroker, IObjectManager objectManager,
        INetwork network, IIssueGenerationRegistry generationRegistry,
        IPlayerManager players, IAwaitingAlternativeSolutionTroopsRegistry returningTroops)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.generationRegistry = generationRegistry;
        this.players = players;
        this.returningTroops = returningTroops;
        messageBroker.Subscribe<NetworkQuestPlayerRemoved>(Handle_PlayerRemoved);
        messageBroker.Subscribe<SmugglersIssueCreated>(Handle_SmugglersIssueCreated);
        messageBroker.Subscribe<NetworkSmugglersIssueCreated>(Handle_NetworkSmugglersIssueCreated);
        messageBroker.Subscribe<SmugglersQuestLogAdded>(Handle_SmugglersQuestLogAdded);
        messageBroker.Subscribe<NetworkSmugglersQuestLog>(Handle_NetworkSmugglersQuestLog);
        messageBroker.Subscribe<SmugglersAlternativeJournalChanged>(Handle_AlternativeJournalChanged);
        messageBroker.Subscribe<NetworkSmugglersAlternativeJournal>(Handle_NetworkAlternativeJournal);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkQuestPlayerRemoved>(Handle_PlayerRemoved);
        messageBroker.Unsubscribe<SmugglersIssueCreated>(Handle_SmugglersIssueCreated);
        messageBroker.Unsubscribe<NetworkSmugglersIssueCreated>(Handle_NetworkSmugglersIssueCreated);
        messageBroker.Unsubscribe<SmugglersQuestLogAdded>(Handle_SmugglersQuestLogAdded);
        messageBroker.Unsubscribe<NetworkSmugglersQuestLog>(Handle_NetworkSmugglersQuestLog);
        messageBroker.Unsubscribe<SmugglersAlternativeJournalChanged>(Handle_AlternativeJournalChanged);
        messageBroker.Unsubscribe<NetworkSmugglersAlternativeJournal>(Handle_NetworkAlternativeJournal);
    }

    private void Handle_PlayerRemoved(MessagePayload<NetworkQuestPlayerRemoved> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (players.TryGetPlayer(data.ControllerId, out var player) && player.HeroId != data.HeroId) return;
            returningTroops.Clear(data.ControllerId);
        });
    }

    private void Handle_AlternativeJournalChanged(MessagePayload<SmugglersAlternativeJournalChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        network.SendAll(new NetworkSmugglersAlternativeJournal(ownerId, issue.StringId,
            issue.JournalEntries.Select(log => new SmugglersJournalEntry(log)).ToArray(),
            issue._areIssueEffectsResolved, payload.What.Status));
    }

    private void Handle_NetworkAlternativeJournal(MessagePayload<NetworkSmugglersAlternativeJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue is not SmugglersIssueBehavior.SmugglersIssue issue
                || issue.StringId != data.IssueId || !issue.IsSolvingWithAlternative) return;
            using (new AllowedThread())
            {
                issue._journalEntries.Clear();
                foreach (var entry in data.Entries) issue._journalEntries.Add(entry.ToJournalLog());
                issue._areIssueEffectsResolved = data.EffectsResolved;
                // Only the journal listener runs here; other issue listeners award server-owned consequences.
                var journal = Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>();
                journal.OnIssueLogAdded(issue, true);
                journal.GetRelatedLog(issue).Update(issue.JournalEntries, data.Status);
            }
        });
    }

    private void Handle_SmugglersQuestLogAdded(MessagePayload<SmugglersQuestLogAdded> payload)
    {
        if (ModInformation.IsClient) return;
        var quest = payload.What.Quest;
        if (!objectManager.TryGetIdWithLogging(quest.QuestGiver, out var ownerId)) return;
        network.SendAll(new NetworkSmugglersQuestLog(ownerId, quest.StringId, quest.JournalEntries.Count - 1,
            payload.What.Log.LogTime.NumTicks, payload.What.Log.LogText));
    }

    private void Handle_NetworkSmugglersQuestLog(MessagePayload<NetworkSmugglersQuestLog> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue?.IssueQuest is not SmugglersIssueBehavior.SmugglersIssueQuest quest || quest.StringId != data.QuestId) return;
            if (quest.JournalEntries.Count > data.EntryIndex) return;
            if (quest.JournalEntries.Count != data.EntryIndex)
                throw new System.InvalidOperationException("Smugglers journal entry arrived before an earlier entry");

            using (new AllowedThread())
            {
                quest._journalEntries.Add(new JournalLog(new CampaignTime(data.TimeTicks), data.Text));
                CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
            }
        });
    }

    private void Handle_SmugglersIssueCreated(MessagePayload<SmugglersIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(issue._targetSettlement, out var targetId)) return;
        if (!objectManager.TryGetIdWithLogging(issue._originSettlement, out var originId)) return;

        var generation = generationRegistry.Bump(issue.IssueOwner);
        network.SendAll(new NetworkSmugglersIssueCreated(ownerId, targetId, originId, generation,
            issue.StringId, issue.IssueCreationTime.NumTicks, issue.IssueDueTime.NumTicks));
    }

    private void Handle_NetworkSmugglersIssueCreated(MessagePayload<NetworkSmugglersIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (generationRegistry.TryGetGeneration(owner, out var generation) && generation >= data.Generation) return;
            if (owner.Issue != null) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.TargetSettlementId, out var target)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.OriginSettlementId, out var origin)) return;

            using (new AllowedThread())
            {
                var issue = new SmugglersIssueBehavior.SmugglersIssue(owner,
                    new KeyValuePair<Settlement, Settlement>(target, origin));
                PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero giver) => issue;
                var potential = new PotentialIssueData(factory, typeof(SmugglersIssueBehavior.SmugglersIssue), IssueBase.IssueFrequency.Rare);
                Campaign.Current.IssueManager.CreateNewIssue(in potential, owner);
                issue.StringId = data.IssueId;
                issue.IssueCreationTime = new CampaignTime(data.CreationTimeTicks);
                issue.IssueDueTime = new CampaignTime(data.DueTimeTicks);
                generationRegistry.SetGeneration(owner, data.Generation);
            }
        });
    }
}
