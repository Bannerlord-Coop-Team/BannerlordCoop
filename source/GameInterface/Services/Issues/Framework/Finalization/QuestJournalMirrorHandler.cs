using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>Observes progress on an already running local quest.</summary>
public readonly struct QuestJournalProgressChanged : IEvent
{
    public readonly JournalLog Log;

    public QuestJournalProgressChanged(JournalLog log) => Log = log;
}

/// <summary>Observes a native journal mutation on the owner's running quest.</summary>
public readonly struct QuestJournalChanged : IEvent
{
    public readonly QuestBase Quest;

    public QuestJournalChanged(QuestBase quest) => Quest = quest;
}

/// <summary>Keeps owner-created history and progress in the unstarted authoritative mirror.</summary>
internal class QuestJournalMirrorHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver resolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IQuestTypeRegistry registry;
    private readonly IControllerIdProvider controller;
    private readonly IPlayerManager players;
    private readonly ConditionalWeakTable<QuestBase, HashSet<JournalLog>> ownerEntries = new();

    public QuestJournalMirrorHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
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
        broker.Subscribe<QuestJournalProgressChanged>(HandleProgress);
        broker.Subscribe<QuestJournalChanged>(HandleChanged);
        broker.Subscribe<RequestQuestJournal>(HandleRequest);
        broker.Subscribe<CampaignReady>(HandleCampaignReady);
    }

    public void Dispose()
    {
        broker.Unsubscribe<QuestJournalProgressChanged>(HandleProgress);
        broker.Unsubscribe<QuestJournalChanged>(HandleChanged);
        broker.Unsubscribe<RequestQuestJournal>(HandleRequest);
        broker.Unsubscribe<CampaignReady>(HandleCampaignReady);
    }

    private void HandleCampaignReady(MessagePayload<CampaignReady> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            // Loaded stages were already shared with the owner before the restart.
            foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
            {
                if (issue.IssueQuest != null && registry.IsRegistered(issue.GetType()))
                    ownerEntries.GetValue(issue.IssueQuest, quest => new HashSet<JournalLog>(quest.JournalEntries));
            }
        }, context: nameof(QuestJournalMirrorHandler));
    }

    private void HandleChanged(MessagePayload<QuestJournalChanged> payload) => SendJournal(payload.What.Quest);

    private void HandleProgress(MessagePayload<QuestJournalProgressChanged> payload)
    {
        if (ModInformation.IsServer || Campaign.Current == null) return;
        var quest = Campaign.Current.QuestManager.Quests.FirstOrDefault(candidate => candidate.JournalEntries.Contains(payload.What.Log));
        if (quest != null) SendJournal(quest);
    }

    private void SendJournal(QuestBase quest)
    {
        if (ModInformation.IsServer || !quest.IsOngoing || Campaign.Current == null ||
            !Campaign.Current.QuestManager.Quests.Contains(quest)) return;
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !registry.IsRegistered(issue.GetType()) ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId) ||
            !ownership.TryGetOwner(giverId, issue.StringId, out var owner) || owner != controller.ControllerId) return;

        network.SendAll(new RequestQuestJournal(giverId, issue.StringId,
            quest.JournalEntries.Select(log => new IssueJournalEntry(log)).ToArray()));
    }

    private void HandleRequest(MessagePayload<RequestQuestJournal> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer) || !players.TryGetPlayer(peer, out var player)) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var owner) || owner != player.ControllerId ||
                !resolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                issue.IssueQuest == null || !issue.IssueQuest.IsOngoing ||
                !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return;

            if (!Reconcile(issue.IssueQuest, data.Entries ?? Array.Empty<IssueJournalEntry>(),
                ownerEntries.GetOrCreateValue(issue.IssueQuest))) return;
            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
                CampaignEventDispatcher.Instance.OnQuestLogAdded(issue.IssueQuest, true);
        }, context: nameof(QuestJournalMirrorHandler));
    }

    private static bool Reconcile(QuestBase quest, IssueJournalEntry[] entries, HashSet<JournalLog> previousOwnerEntries)
    {
        var remaining = quest.JournalEntries.ToList();
        var ordered = new List<JournalLog>();
        var updates = new List<(JournalLog Log, int Progress)>();
        foreach (var entry in entries)
        {
            var matches = remaining.Where(log => log.Type == entry.Type && log.Range == entry.Range &&
                SameText(log.LogText, entry.Text) && SameText(log.TaskName, entry.TaskName)).ToArray();
            if (matches.Length == 0)
                matches = remaining.Where(log => log.LogText?.ToString() == entry.Text?.ToString()).ToArray();
            var exact = matches.Where(log => log.LogTime.NumTicks == entry.Time.NumTicks).ToArray();
            // Existing server-created stages may have a different tick, but ambiguous occurrences cannot be reassigned.
            if (exact.Length > 1 || (exact.Length == 0 && matches.Length > 1)) return false;
            var existing = exact.FirstOrDefault() ?? matches.FirstOrDefault();
            if (existing == null) ordered.Add(entry.ToJournalLog());
            else
            {
                remaining.Remove(existing);
                ordered.Add(existing);
                updates.Add((existing, entry.Progress));
            }
        }
        // Only omitted stages previously mirrored from the owner are removals; retain unseen authoritative stages.
        var currentOwnerEntries = ordered.ToArray();
        ordered.AddRange(remaining.Where(log => !previousOwnerEntries.Contains(log)));
        previousOwnerEntries.Clear();
        previousOwnerEntries.UnionWith(currentOwnerEntries);
        if (ordered.SequenceEqual(quest.JournalEntries) && updates.All(update => update.Log.CurrentProgress == update.Progress)) return false;
        foreach (var update in updates) update.Log.UpdateCurrentProgress(update.Progress);
        quest._journalEntries.Clear();
        quest._journalEntries.AddRange(ordered);
        return true;
    }

    private static bool SameText(TextObject first, TextObject second)
    {
        if (ReferenceEquals(first, second)) return true;
        if (first == null || second == null || first.Value != second.Value ||
            (first.Attributes?.Count ?? 0) != (second.Attributes?.Count ?? 0)) return false;
        if (first.Attributes == null) return true;
        foreach (var pair in first.Attributes)
        {
            if (second.Attributes == null || !second.Attributes.TryGetValue(pair.Key, out var value)) return false;
            if (pair.Value is TextObject text)
            {
                if (!(value is TextObject other) || !SameText(text, other)) return false;
            }
            else if (!Equals(pair.Value, value)) return false;
        }
        return true;
    }
}
