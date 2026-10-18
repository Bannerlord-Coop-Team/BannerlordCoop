using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
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
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;

    public RaidAnEnemyTerritoryProgressHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        messageBroker.Subscribe<RaidAnEnemyTerritoryProgressReady>(HandleProgressReady);
        messageBroker.Subscribe<NetworkRaidAnEnemyTerritoryProgress>(HandleProgress);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RaidAnEnemyTerritoryProgressReady>(HandleProgressReady);
        messageBroker.Unsubscribe<NetworkRaidAnEnemyTerritoryProgress>(HandleProgress);
    }

    private void HandleProgressReady(MessagePayload<RaidAnEnemyTerritoryProgressReady> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        var data = payload.What;
        network.SendAll(new NetworkRaidAnEnemyTerritoryProgress(
            data.IssueOwnerId,
            data.IssueId,
            data.VillageIds,
            data.Entries));
    }

    private void HandleProgress(MessagePayload<NetworkRaidAnEnemyTerritoryProgress> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest))
            {
                return;
            }

            var villages = (data.VillageIds ?? Array.Empty<string>())
                .Select(id => objectManager.TryGetObjectWithLogging<Settlement>(id, out var village) ? village : null)
                .ToArray();
            if (villages.Any(village => village == null))
            {
                return;
            }

            using (new AllowedThread())
            {
                quest._raidedVillages.Clear();
                quest._raidedVillages.AddRange(villages);
                quest._journalEntries.Clear();
                foreach (var entry in data.Entries ?? Array.Empty<RaidAnEnemyTerritoryJournalEntry>())
                {
                    quest._journalEntries.Add(new JournalLog(entry.Time, entry.Text, entry.Task, entry.Progress, entry.Range, entry.Type));
                }

                quest._raidedVillagesTrackLog = quest.JournalEntries.FirstOrDefault(log => log.Type == LogType.Discreate);
                CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
            }
        }, context: nameof(RaidAnEnemyTerritoryProgressHandler));
    }
}
