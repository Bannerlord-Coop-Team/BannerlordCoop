using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Handlers;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

internal class TheConquestOfSettlementHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly INetwork network;
    private readonly IIssueGenerationRegistry generations;

    public TheConquestOfSettlementHandler(IMessageBroker broker, IObjectManager objects, INetwork network, IIssueGenerationRegistry generations)
    {
        this.broker = broker;
        this.objects = objects;
        this.network = network;
        this.generations = generations;
        broker.Subscribe<ConquestIssueCreated>(HandleCreated);
        broker.Subscribe<NetworkConquestIssueCreated>(HandleReceivedCreation);
        broker.Subscribe<ConquestQuestFinalizing>(HandleFinalizing);
        broker.Subscribe<NetworkConquestQuestJournal>(HandleReceivedJournal);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ConquestIssueCreated>(HandleCreated);
        broker.Unsubscribe<NetworkConquestIssueCreated>(HandleReceivedCreation);
        broker.Unsubscribe<ConquestQuestFinalizing>(HandleFinalizing);
        broker.Unsubscribe<NetworkConquestQuestJournal>(HandleReceivedJournal);
    }

    private void HandleCreated(MessagePayload<ConquestIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        using (new AllowedThread())
        {
            issue.StringId = "coop_conquest_" + issue.StringId;
        }
        if (!objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        if (!objects.TryGetIdWithLogging(issue._targetSettlement, out var targetId)) return;
        network.SendAll(new NetworkConquestIssueCreated(giverId, targetId, generations.Bump(issue.IssueOwner), issue.IssueDueTime, issue.StringId));
    }

    private void HandleReceivedCreation(MessagePayload<NetworkConquestIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (!objects.TryGetObjectWithLogging<Settlement>(data.TargetId, out var target)) return;
            if (generations.TryGetGeneration(giver, out var generation) && generation >= data.Generation) return;
            if (giver.Issue != null || string.IsNullOrEmpty(data.IssueId)) return;
            using (new AllowedThread())
            {
                var issue = new Issue(giver, target) { IssueDueTime = data.DueTime };
                PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero owner) => issue;
                var potential = new PotentialIssueData(factory, typeof(Issue), IssueBase.IssueFrequency.VeryCommon);
                if (Campaign.Current.IssueManager.CreateNewIssue(in potential, giver))
                {
                    issue.StringId = data.IssueId;
                    generations.SetGeneration(giver, data.Generation);
                }
            }
        });
    }

    private void HandleFinalizing(MessagePayload<ConquestQuestFinalizing> payload)
    {
        if (ModInformation.IsClient) return;
        var quest = payload.What.Quest;
        if (!objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId)) return;
        if (!generations.TryGetGeneration(quest.QuestGiver, out var generation)) return;
        network.SendAll(new NetworkConquestQuestJournal(giverId, quest.StringId, generation,
            quest.JournalEntries.Select(log => log.LogText).ToArray(), quest.JournalEntries.Select(log => log.LogTime).ToArray()));
    }

    private void HandleReceivedJournal(MessagePayload<NetworkConquestQuestJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (!generations.TryGetGeneration(giver, out var generation) || generation != data.Generation) return;
            if (giver.Issue?.IssueQuest is not Quest quest || quest.StringId != data.QuestId) return;
            if (data.Texts == null || data.Times == null || data.Texts.Length != data.Times.Length) return;
            quest._journalEntries.Clear();
            for (var index = 0; index < data.Texts.Length; index++)
                quest._journalEntries.Add(new JournalLog(data.Times[index], data.Texts[index]));
        });
    }
}
