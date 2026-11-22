using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

internal sealed class RaidAnEnemyTerritoryProgressHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;
    private readonly IIssueOwnerResolver owners;
    private readonly IIssueConversationGuard conversationGuard;

    public RaidAnEnemyTerritoryProgressHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnerResolver owners,
        IIssueConversationGuard conversationGuard)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.owners = owners;
        this.conversationGuard = conversationGuard;
        messageBroker.Subscribe<NetworkRaidAnEnemyTerritoryProgress>(HandleProgress);
    }

    public void Dispose() => messageBroker.Unsubscribe<NetworkRaidAnEnemyTerritoryProgress>(HandleProgress);

    private void HandleProgress(MessagePayload<NetworkRaidAnEnemyTerritoryProgress> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() => Apply(data), data.IssueOwnerId),
            context: nameof(RaidAnEnemyTerritoryProgressHandler));
    }

    private void Apply(NetworkRaidAnEnemyTerritoryProgress data)
    {
        if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
            issue.IssueQuest is not Quest quest ||
            !owners.IsLocalOwner(issue))
        {
            return;
        }

        var villageIds = data.VillageIds ?? Array.Empty<string>();
        var villages = new Settlement[villageIds.Length];
        for (var index = 0; index < villageIds.Length; index++)
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(villageIds[index], out var village))
            {
                return;
            }
            villages[index] = village;
        }

        using (new AllowedThread())
        using (new QuestJournalReplay())
        {
            quest._raidedVillages.Clear();
            quest._raidedVillages.AddRange(villages);
            var entries = data.Entries ?? Array.Empty<IssueJournalEntry>();
            if (entries.Length > 0)
            {
                quest._journalEntries.Clear();
                foreach (var entry in entries)
                {
                    quest._journalEntries.Add(entry.ToJournalLog());
                }
                quest._raidedVillagesTrackLog = quest.JournalEntries.FirstOrDefault(log => log.Type == LogType.Discreate);
            }

            quest._raidedVillagesTrackLog?.UpdateCurrentProgress(quest._raidedVillages.Count);
            CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
        }
    }
}
