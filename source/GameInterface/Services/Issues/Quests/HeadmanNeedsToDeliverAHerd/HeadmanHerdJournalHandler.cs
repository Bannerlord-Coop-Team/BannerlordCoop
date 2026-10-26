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
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanHerdLog : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly TextObject Text;

    public NetworkHeadmanHerdLog(string giverId, string issueId, TextObject text)
    {
        GiverId = giverId;
        IssueId = issueId;
        Text = text;
    }
}

public readonly struct HeadmanHerdLogAdded : IEvent
{
    internal readonly Quest Quest;
    internal readonly TextObject Text;

    internal HeadmanHerdLogAdded(Quest quest, TextObject text)
    {
        Quest = quest;
        Text = text;
    }
}

internal class HeadmanHerdJournalHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeadmanHerdJournalHandler>();
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controllerId;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;

    public HeadmanHerdJournalHandler(IMessageBroker broker, INetwork network, IObjectManager objects, IIssueResolver issues, IControllerIdProvider controllerId, IIssueOwnershipRegistry ownership, IPlayerManager players)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.issues = issues;
        this.controllerId = controllerId;
        this.ownership = ownership;
        this.players = players;
        broker.Subscribe<HeadmanHerdLogAdded>(Send);
        broker.Subscribe<NetworkHeadmanHerdLog>(Receive);
        broker.Subscribe<NetworkQuestSolutionAccepted>(ShowGrant);
    }

    public void Dispose()
    {
        broker.Unsubscribe<HeadmanHerdLogAdded>(Send);
        broker.Unsubscribe<NetworkHeadmanHerdLog>(Receive);
        broker.Unsubscribe<NetworkQuestSolutionAccepted>(ShowGrant);
    }

    private void ShowGrant(MessagePayload<NetworkQuestSolutionAccepted> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controllerId.ControllerId) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) || !(issue.IssueQuest is Quest quest)) return;
            var text = new TextObject("{=GgBtpOEm}{.%}{ANIMAL_COUNT} {?ANIMAL_COUNT > 1}{PLURAL(ANIMAL_TYPE)}{?}{ANIMAL_TYPE}{\\?}{.%} added to your party.");
            text.SetTextVariable("ANIMAL_COUNT", quest._animalCountToDeliver);
            text.SetTextVariable("ANIMAL_TYPE", quest._herdTypeToDeliver.Name);
            MBInformationManager.AddQuickInformation(text);
        }, context: nameof(HeadmanHerdJournalHandler));
    }

    private void Send(MessagePayload<HeadmanHerdLogAdded> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = IssueManager.GetIssueOfQuest(payload.What.Quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        if (!ownership.TryGetOwner(giverId, issue.StringId, out var owner)) return;
        if (!players.TryGetPeer(owner, out var peer)) return;
        network.Send(peer, new NetworkHeadmanHerdLog(giverId, issue.StringId, payload.What.Text));
    }

    private void Receive(MessagePayload<NetworkHeadmanHerdLog> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!ownership.TryGetOwner(data.GiverId, data.IssueId, out var owner) || owner != controllerId.ControllerId ||
                !issues.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _) || !(issue.IssueQuest is Quest quest) ||
                !Campaign.Current.QuestManager.Quests.Contains(quest)) return;
            var existing = quest.JournalEntries.Where(log => log.LogText != null)
                .Select(log => $"{log.LogText.GetID()}:{log.LogText}").ToArray();
            Logger.Information("herd journal receive {issue} {textId}:{text} existing={existing}", data.IssueId,
                data.Text?.GetID(), data.Text, string.Join("|", existing));
            if (data.Text != null && quest.JournalEntries.Any(log =>
                    log.LogText?.GetID() == data.Text.GetID() || log.LogText?.ToString() == data.Text.ToString())) return;
            using (new AllowedThread())
            using (new QuestJournalReplay())
                quest.AddLog(data.Text);
        }, context: nameof(HeadmanHerdJournalHandler));
    }
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
internal class HeadmanHerdJournalPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, TextObject text)
    {
        if (ModInformation.IsServer && __instance is Quest quest)
            MessageBroker.Instance.Publish(quest, new HeadmanHerdLogAdded(quest, text));
    }
}
