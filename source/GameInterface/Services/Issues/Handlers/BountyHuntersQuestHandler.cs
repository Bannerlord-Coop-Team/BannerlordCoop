using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Handlers;

internal sealed class BountyHuntersQuestHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly IIssueOwnershipRegistry owners;
    private readonly IIssueGenerationRegistry generations;
    private readonly INetwork network;
    private readonly IPlayerManager players;

    public BountyHuntersQuestHandler(IMessageBroker broker, IObjectManager objects,
        IIssueOwnershipRegistry owners, IIssueGenerationRegistry generations, INetwork network, IPlayerManager players)
    {
        this.broker = broker;
        this.objects = objects;
        this.owners = owners;
        this.generations = generations;
        this.network = network;
        this.players = players;
        broker.Subscribe<BountyHuntersJournalChanged>(SendJournal);
        broker.Subscribe<NetworkBountyHuntersJournal>(ReceiveJournal);
        broker.Subscribe<PlayerHeirSelectionCompleted>(PlayerChanged);
    }

    public void Dispose()
    {
        broker.Unsubscribe<BountyHuntersJournalChanged>(SendJournal);
        broker.Unsubscribe<NetworkBountyHuntersJournal>(ReceiveJournal);
        broker.Unsubscribe<PlayerHeirSelectionCompleted>(PlayerChanged);
    }

    private void PlayerChanged(MessagePayload<PlayerHeirSelectionCompleted> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetIdWithLogging(payload.What.PlayerHero, out var heroId)) return;
            var player = players.Players.FirstOrDefault(candidate => candidate.HeroId == heroId);
            if (player == null) return;
            foreach (var entry in owners.Snapshot().Where(entry => entry.Value == player.ControllerId))
            {
                if (entry.Key.Issue?.IssueQuest is CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest quest)
                    quest.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan."));
            }
        });
    }

    private void SendJournal(MessagePayload<BountyHuntersJournalChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!owners.TryGetOwnerControllerId(issue.IssueOwner, out _)) return;
        if (!objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        if (!generations.TryGetGeneration(issue.IssueOwner, out var generation)) return;
        var entries = payload.What.QuestJournal ? issue.IssueQuest?.JournalEntries : issue.JournalEntries;
        if (entries == null) return;
        network.SendAll(new NetworkBountyHuntersJournal(giverId, generation, payload.What.QuestJournal, entries, payload.What.Status));
    }

    private void ReceiveJournal(MessagePayload<NetworkBountyHuntersJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (giver.Issue is not CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue issue) return;
            if (!generations.TryGetGeneration(giver, out var generation) || generation != data.Generation) return;
            if (!owners.IsLocalPeerOwner(giver) || data.Entries == null) return;
            using (new AllowedThread())
            {
                if (data.QuestJournal)
                {
                    if (issue.IssueQuest is not CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest quest) return;
                    quest._journalEntries.Clear();
                    foreach (var entry in data.Entries) quest._journalEntries.Add(entry.ToJournalLog());
                    CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, false);
                }
                else
                {
                    issue._journalEntries.Clear();
                    foreach (var entry in data.Entries) issue._journalEntries.Add(entry.ToJournalLog());
                    CampaignEventDispatcher.Instance.OnIssueLogAdded(issue, false);
                }
                if (data.Status != IssueBase.IssueUpdateDetails.None)
                    CampaignEventDispatcher.Instance.OnIssueUpdated(issue, data.Status, Hero.MainHero);
            }
        });
    }
}
