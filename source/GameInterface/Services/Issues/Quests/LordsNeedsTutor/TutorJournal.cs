using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using ProtoBuf;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor;

internal interface ITutorJournal
{
    void Send(Quest quest);
    void NotifyReward(TextObject message);
}

internal sealed class TutorJournal : ITutorJournal
{
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IPlayerManager players;
    private readonly INetwork network;

    public TutorJournal(IObjectManager objects, IIssueOwnerResolver ownerResolver, IPlayerManager players, INetwork network)
    {
        this.objects = objects;
        this.ownerResolver = ownerResolver;
        this.players = players;
        this.network = network;
    }

    public void NotifyReward(TextObject message)
    {
        if (ModInformation.IsClient || !objects.TryGetId(Hero.MainHero, out var heroId)) return;
        var player = players.Players.FirstOrDefault(candidate => candidate.HeroId == heroId);
        if (player != null && players.TryGetPeer(player.ControllerId, out var peer))
            network.Send(peer, new NetworkTutorReward { Message = message });
    }

    public void Send(Quest quest)
    {
        if (ModInformation.IsClient || quest._startQuestLog == null) return;
        var issue = TaleWorlds.CampaignSystem.Issues.IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !objects.TryGetId(quest.QuestGiver, out var giverId)) return;
        if (!ownerResolver.TryGetOwnerControllerId(issue, out var controller))
        {
            var owner = quest._youngHero.CompanionOf?.Leader;
            if (owner == null || !objects.TryGetId(owner, out var ownerId)) return;
            controller = players.Players.FirstOrDefault(player => player.HeroId == ownerId)?.ControllerId;
            if (controller == null) return;
        }
        if (!players.TryGetPeer(controller, out var peer)) return;
        network.Send(peer, new NetworkTutorJournal
        {
            GiverId = giverId, IssueId = issue.StringId,
            FirstConversation = quest._firstConversationInitialized, Ready = quest._questCompletedStartConversation,
            Failed = quest._showQuestFailedConversation,
            Entries = quest.JournalEntries.Select(entry => new TutorJournalEntry
            {
                Time = entry.LogTime, Text = entry.LogText, Task = entry.TaskName,
                Progress = entry.CurrentProgress, Range = entry.Range, Type = entry.Type,
            }).ToArray(),
        });
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkTutorJournal : IServerToClientCommand
{
    [ProtoMember(1)] public string GiverId;
    [ProtoMember(2)] public string IssueId;
    [ProtoMember(3)] public bool FirstConversation;
    [ProtoMember(4)] public bool Ready;
    [ProtoMember(5)] public bool Failed;
    [ProtoMember(6)] public TutorJournalEntry[] Entries;
}

[ProtoContract(SkipConstructor = true)]
public sealed class TutorJournalEntry
{
    [ProtoMember(1)] public CampaignTime Time;
    [ProtoMember(2)] public TextObject Text;
    [ProtoMember(3)] public TextObject Task;
    [ProtoMember(4)] public int Progress;
    [ProtoMember(5)] public int Range;
    [ProtoMember(6)] public LogType Type;
}

internal sealed class TutorJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;

    public TutorJournalHandler(IMessageBroker broker, IObjectManager objects)
    {
        this.broker = broker;
        this.objects = objects;
        broker.Subscribe<NetworkTutorJournal>(Handle);
        broker.Subscribe<NetworkTutorReward>(HandleReward);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkTutorJournal>(Handle);
        broker.Unsubscribe<NetworkTutorReward>(HandleReward);
    }

    private void HandleReward(MessagePayload<NetworkTutorReward> payload)
    {
        if (ModInformation.IsClient)
            GameThread.RunSafe(() => TaleWorlds.Core.MBInformationManager.AddQuickInformation(payload.What.Message), context: nameof(TutorJournalHandler));
    }

    private void Handle(MessagePayload<NetworkTutorJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver) ||
                giver.Issue?.StringId != data.IssueId || giver.Issue.IssueQuest is not Quest quest || !quest.IsOngoing) return;
            quest._firstConversationInitialized = data.FirstConversation;
            quest._questCompletedStartConversation = data.Ready;
            quest._showQuestFailedConversation = data.Failed;
            quest._journalEntries.Clear();
            foreach (var entry in data.Entries)
                quest._journalEntries.Add(new JournalLog(entry.Time, entry.Text, entry.Task, entry.Progress, entry.Range, entry.Type));
            quest._startQuestLog = quest._journalEntries.FirstOrDefault();
            CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
        }, context: nameof(TutorJournalHandler));
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkTutorReward : IServerToClientCommand
{
    [ProtoMember(1)] public TextObject Message;
}
