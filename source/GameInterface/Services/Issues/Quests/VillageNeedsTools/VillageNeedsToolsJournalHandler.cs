using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using ProtoBuf;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsTools;

/// <summary>Delivers the server's ending logs before the framework finalizes the owner's quest.</summary>
internal class VillageNeedsToolsJournalHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IIssueResolver issueResolver;
    private readonly IPlayerManager players;

    public VillageNeedsToolsJournalHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, IIssueOwnershipRegistry ownership,
        IIssueResolver issueResolver, IPlayerManager players)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.ownership = ownership;
        this.issueResolver = issueResolver;
        this.players = players;
        messageBroker.Subscribe<VillageNeedsToolsJournalAdded>(Handle_Added);
        messageBroker.Subscribe<NetworkVillageNeedsToolsJournal>(Handle_Journal);
        messageBroker.Subscribe<VillageNeedsToolsAlternativeEnding>(Handle_AlternativeEnding);
        messageBroker.Subscribe<NetworkVillageNeedsToolsAlternativeJournal>(Handle_AlternativeJournal);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageNeedsToolsJournalAdded>(Handle_Added);
        messageBroker.Unsubscribe<NetworkVillageNeedsToolsJournal>(Handle_Journal);
        messageBroker.Unsubscribe<VillageNeedsToolsAlternativeEnding>(Handle_AlternativeEnding);
        messageBroker.Unsubscribe<NetworkVillageNeedsToolsAlternativeJournal>(Handle_AlternativeJournal);
    }

    private void Handle_Added(MessagePayload<VillageNeedsToolsJournalAdded> payload)
    {
        if (ModInformation.IsClient) return;

        var quest = payload.What.Quest;
        var issue = quest.QuestGiver.Issue;
        if (issue == null || !objectManager.TryGetIdWithLogging(quest.QuestGiver, out var giverId)
            || !ownership.TryGetOwner(giverId, issue.StringId, out var controllerId)
            || !players.TryGetPeer(controllerId, out var peer)) return;

        network.Send(peer, new NetworkVillageNeedsToolsJournal(giverId, issue.StringId, payload.What.Text));
    }

    private void Handle_Journal(MessagePayload<NetworkVillageNeedsToolsJournal> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _)
                || !(issue.IssueQuest is Quest quest) || (!quest.IsOngoing && !quest.IsFinalized)) return;

            if (!quest.JournalEntries.Any(log => log.LogText?.ToString() == data.Text))
                quest.AddLog(new TextObject("{=!}" + data.Text));
        }, context: nameof(VillageNeedsToolsJournalHandler));
    }

    private void Handle_AlternativeEnding(MessagePayload<VillageNeedsToolsAlternativeEnding> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var giverId)
            || !ownership.TryGetOwner(giverId, issue.StringId, out var controllerId)
            || !players.TryGetPeer(controllerId, out var peer)) return;

        network.Send(peer, new NetworkVillageNeedsToolsAlternativeJournal(giverId, issue.StringId,
            issue.JournalEntries.Select(log => log.LogText?.ToString() ?? string.Empty).ToArray(),
            issue.JournalEntries.Select(log => log.LogTime.NumTicks).ToArray(), payload.What.Status));
    }

    private void Handle_AlternativeJournal(MessagePayload<NetworkVillageNeedsToolsAlternativeJournal> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.GiverId, data.IssueId, out _, out var resolved, out _)
                || !(resolved is Issue issue) || !issue.IsSolvingWithAlternative) return;

            var texts = data.Texts ?? Array.Empty<string>();
            var times = data.Times ?? Array.Empty<long>();
            if (texts.Length != times.Length) return;

            issue._journalEntries.Clear();
            for (var i = 0; i < texts.Length; i++)
                issue._journalEntries.Add(new JournalLog(new CampaignTime(times[i]), new TextObject("{=!}" + texts[i])));

            // Finalization only detaches the issue, so retain its owner-only ending in the native history first.
            var archived = Campaign.Current.LogEntryHistory.FindLastGameActionLog(
                (JournalLogEntry entry) => entry.IsRelatedTo(issue));
            if (archived == null)
            {
                archived = new JournalLogEntry(issue.Title, issue.IssueOwner, issue.CounterOfferHero, false, issue);
                LogEntry.AddLogEntry(archived);
            }
            archived.Update(issue.JournalEntries, data.Status);
        }, context: nameof(VillageNeedsToolsJournalHandler));
    }
}

public readonly struct VillageNeedsToolsAlternativeEnding : IEvent
{
    public readonly Issue Issue;
    public readonly IssueBase.IssueUpdateDetails Status;

    public VillageNeedsToolsAlternativeEnding(Issue issue, IssueBase.IssueUpdateDetails status)
    {
        Issue = issue;
        Status = status;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageNeedsToolsAlternativeJournal : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(4)] public readonly string[] Texts;
    [ProtoMember(5)] public readonly long[] Times;
    [ProtoMember(6)] public readonly IssueBase.IssueUpdateDetails Status;

    public NetworkVillageNeedsToolsAlternativeJournal(string giverId, string issueId,
        string[] texts, long[] times, IssueBase.IssueUpdateDetails status)
    {
        GiverId = giverId;
        IssueId = issueId;
        Texts = texts;
        Times = times;
        Status = status;
    }
}

public readonly struct VillageNeedsToolsJournalAdded : IEvent
{
    public readonly Quest Quest;
    public readonly string Text;

    public VillageNeedsToolsJournalAdded(Quest quest, string text)
    {
        Quest = quest;
        Text = text;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageNeedsToolsJournal : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(4)] public readonly string Text;

    public NetworkVillageNeedsToolsJournal(string giverId, string issueId, string text)
    {
        GiverId = giverId;
        IssueId = issueId;
        Text = text;
    }
}
