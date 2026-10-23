using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Finalization;

public readonly struct QuestJournalProgressChanged : IEvent
{
    public readonly JournalLog Log;

    public QuestJournalProgressChanged(JournalLog log) => Log = log;
}

/// <summary>Keeps the owner's discrete progress in the mirror before a server-driven ending archives it.</summary>
internal class QuestJournalProgressHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver resolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IQuestTypeRegistry registry;
    private readonly IControllerIdProvider controller;
    private readonly IPlayerManager players;

    public QuestJournalProgressHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueResolver resolver, IIssueOwnershipRegistry ownership, IQuestTypeRegistry registry,
        IControllerIdProvider controller, IPlayerManager players)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.resolver = resolver;
        this.ownership = ownership;
        this.registry = registry;
        this.controller = controller;
        this.players = players;
        broker.Subscribe<QuestJournalProgressChanged>(HandleChanged);
        broker.Subscribe<RequestQuestJournalProgress>(HandleRequest);
    }

    public void Dispose()
    {
        broker.Unsubscribe<QuestJournalProgressChanged>(HandleChanged);
        broker.Unsubscribe<RequestQuestJournalProgress>(HandleRequest);
    }

    private void HandleChanged(MessagePayload<QuestJournalProgressChanged> payload)
    {
        if (ModInformation.IsServer || Campaign.Current == null) return;
        foreach (var quest in Campaign.Current.QuestManager.Quests)
        {
            var index = quest._journalEntries.IndexOf(payload.What.Log);
            if (index < 0) continue;
            var issue = IssueManager.GetIssueOfQuest(quest);
            if (issue == null || !registry.IsRegistered(issue.GetType()) ||
                !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId) ||
                !ownership.TryGetOwner(giverId, issue.StringId, out var owner) || owner != controller.ControllerId) return;

            network.SendAll(new RequestQuestJournalProgress(giverId, issue.StringId, index, payload.What.Log));
            return;
        }
    }

    private void HandleRequest(MessagePayload<RequestQuestJournalProgress> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer) || !players.TryGetPlayer(peer, out var player)) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var owner) || owner != player.ControllerId ||
                !resolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                issue.IssueQuest == null || !issue.IssueQuest.IsOngoing) return;
            var entries = issue.IssueQuest.JournalEntries;
            if (data.Index < 0 || data.Index >= entries.Count) return;
            var log = entries[data.Index];
            if (log.Type != data.Type || log.Range != data.Range ||
                log.LogText?.Value != data.TextValue || log.TaskName?.Value != data.TaskValue) return;
            log.UpdateCurrentProgress(data.Progress);
        }, context: nameof(QuestJournalProgressHandler));
    }
}
