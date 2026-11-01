using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using ProtoBuf;
using System.Collections.Concurrent;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanHerdLog : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly TextObject Text;
    [ProtoMember(4)] public readonly IssueJournalEntry Entry;

    public NetworkHeadmanHerdLog(string giverId, string issueId, TextObject text)
    {
        GiverId = giverId;
        IssueId = issueId;
        Text = text;
        Entry = default;
    }

    public NetworkHeadmanHerdLog(string giverId, string issueId, JournalLog log)
    {
        GiverId = giverId;
        IssueId = issueId;
        Text = null;
        Entry = new IssueJournalEntry(log);
    }
}

public readonly struct HeadmanHerdLogAdded : IEvent
{
    internal readonly Quest Quest;
    internal readonly JournalLog Log;

    internal HeadmanHerdLogAdded(Quest quest, JournalLog log)
    {
        Quest = quest;
        Log = log;
    }
}

internal class HeadmanHerdJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controllerId;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly ConcurrentDictionary<(string GiverId, string IssueId), byte> pendingAccepts = new();
    private readonly IIssueConversationGuard conversationGuard;

    public HeadmanHerdJournalHandler(IMessageBroker broker, INetwork network, IObjectManager objects, IIssueResolver issues, IControllerIdProvider controllerId, IIssueOwnershipRegistry ownership, IPlayerManager players, IIssueConversationGuard conversationGuard)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.issues = issues;
        this.controllerId = controllerId;
        this.ownership = ownership;
        this.players = players;
        this.conversationGuard = conversationGuard;
        broker.Subscribe<HeadmanHerdLogAdded>(Send);
        broker.Subscribe<NetworkHeadmanHerdLog>(Receive);
        broker.Subscribe<QuestSolutionAcceptTriggered>(TrackAccept);
        broker.Subscribe<QuestAcceptanceFinished>(FinishAccept);
        broker.Subscribe<NetworkQuestSolutionAccepted>(HandleAccepted);
        broker.Subscribe<NetworkQuestSolutionAcceptRejected>(HandleRejected);
    }

    public void Dispose()
    {
        broker.Unsubscribe<HeadmanHerdLogAdded>(Send);
        broker.Unsubscribe<NetworkHeadmanHerdLog>(Receive);
        broker.Unsubscribe<QuestSolutionAcceptTriggered>(TrackAccept);
        broker.Unsubscribe<QuestAcceptanceFinished>(FinishAccept);
        broker.Unsubscribe<NetworkQuestSolutionAccepted>(HandleAccepted);
        broker.Unsubscribe<NetworkQuestSolutionAcceptRejected>(HandleRejected);
    }

    private void TrackAccept(MessagePayload<QuestSolutionAcceptTriggered> payload) => TrackAccept(payload.What.Issue);

    private void TrackAccept(IssueBase issue)
    {
        if (ModInformation.IsServer || issue?.IssueQuest is not Quest quest) return;
        if (!objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        pendingAccepts.TryAdd((giverId, issue.StringId), 0);
    }

    private void FinishAccept(MessagePayload<QuestAcceptanceFinished> payload)
    {
        if (ModInformation.IsServer)
            return;

        if (payload.What.Quest == null)
        {
            pendingAccepts.Clear();
            return;
        }

        var issue = IssueManager.GetIssueOfQuest(payload.What.Quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId))
            return;
        pendingAccepts.TryRemove((giverId, issue.StringId), out _);
    }

    private void HandleAccepted(MessagePayload<NetworkQuestSolutionAccepted> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controllerId.ControllerId ||
            !pendingAccepts.TryRemove((payload.What.IssueOwnerId, payload.What.IssueId), out _)) return;

        GameThread.RunSafe(() =>
        {
            if (!issues.TryResolve(payload.What.IssueOwnerId, payload.What.IssueId, out _, out var issue, out _) ||
                issue.IssueQuest is not Quest quest) return;
            var text = new TextObject("{=GgBtpOEm}{.%}{ANIMAL_COUNT} {?ANIMAL_COUNT > 1}{PLURAL(ANIMAL_TYPE)}{?}{ANIMAL_TYPE}{\\?}{.%} added to your party.");
            text.SetTextVariable("ANIMAL_COUNT", quest._animalCountToDeliver);
            text.SetTextVariable("ANIMAL_TYPE", quest._herdTypeToDeliver.Name);
            MBInformationManager.AddQuickInformation(text);
        }, context: nameof(HeadmanHerdJournalHandler));
    }

    private void HandleRejected(MessagePayload<NetworkQuestSolutionAcceptRejected> payload)
    {
        if (ModInformation.IsServer) return;
        pendingAccepts.TryRemove((payload.What.IssueOwnerId, payload.What.IssueId), out _);
    }

    private void Send(MessagePayload<HeadmanHerdLogAdded> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = IssueManager.GetIssueOfQuest(payload.What.Quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        if (!ownership.TryGetOwner(giverId, issue.StringId, out var owner)) return;
        if (!players.TryGetPeer(owner, out var peer)) return;
        network.Send(peer, new NetworkHeadmanHerdLog(giverId, issue.StringId, payload.What.Log));
    }

    private void Receive(MessagePayload<NetworkHeadmanHerdLog> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() =>
        {
            if (!ownership.TryGetOwner(data.GiverId, data.IssueId, out var owner) || owner != controllerId.ControllerId ||
                !issues.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _) || !(issue.IssueQuest is Quest quest) ||
                !Campaign.Current.QuestManager.Quests.Contains(quest)) return;
            var log = data.Entry.Text != null ? data.Entry.ToJournalLog() :
                data.Text == null ? null : new JournalLog(CampaignTime.Now, data.Text);
            if (log == null) return;
            if (data.Entry.Text != null)
            {
                if (quest.JournalEntries.Any(existing => SameLog(existing, log))) return;
            }
            else if (data.Text == null || quest.JournalEntries.Any(existing =>
                         existing.LogText?.ToString() == data.Text.ToString())) return;
            using (new AllowedThread())
            using (new QuestJournalReplay())
                quest._journalEntries.Add(log);
        }, data.GiverId), context: nameof(HeadmanHerdJournalHandler));
    }

    private static bool SameLog(JournalLog first, JournalLog second) =>
        first.LogTime.NumTicks == second.LogTime.NumTicks && first.Range == second.Range &&
        first.CurrentProgress == second.CurrentProgress && first.Type == second.Type &&
        SameText(first.LogText, second.LogText) && SameText(first.TaskName, second.TaskName);

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

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
internal class HeadmanHerdJournalPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, TextObject text, JournalLog __result)
    {
        if (ModInformation.IsServer && __instance is Quest quest)
            MessageBroker.Instance.Publish(quest, new HeadmanHerdLogAdded(quest, __result));
    }
}
