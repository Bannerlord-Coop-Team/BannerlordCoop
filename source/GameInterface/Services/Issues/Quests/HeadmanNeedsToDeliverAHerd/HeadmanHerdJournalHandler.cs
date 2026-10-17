using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHeadmanHerdLog : IServerToClientCommand
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

internal readonly record struct HeadmanHerdLogAdded(Quest Quest, TextObject Text) : IEvent;

internal class HeadmanHerdJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;

    public HeadmanHerdJournalHandler(IMessageBroker broker, INetwork network, IObjectManager objects, IIssueResolver issues)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.issues = issues;
        broker.Subscribe<HeadmanHerdLogAdded>(Send);
        broker.Subscribe<NetworkHeadmanHerdLog>(Receive);
    }

    public void Dispose()
    {
        broker.Unsubscribe<HeadmanHerdLogAdded>(Send);
        broker.Unsubscribe<NetworkHeadmanHerdLog>(Receive);
    }

    private void Send(MessagePayload<HeadmanHerdLogAdded> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = IssueManager.GetIssueOfQuest(payload.What.Quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        network.SendAll(new NetworkHeadmanHerdLog(giverId, issue.StringId, payload.What.Text));
    }

    private void Receive(MessagePayload<NetworkHeadmanHerdLog> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issues.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _) || !(issue.IssueQuest is Quest quest)) return;
            using (new AllowedThread()) quest.AddLog(data.Text);
        }, context: nameof(HeadmanHerdJournalHandler));
    }
}

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
