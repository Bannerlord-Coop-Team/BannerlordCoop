using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPoachersJournalLog : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string Text;

    public NetworkPoachersJournalLog(string issueOwnerId, string issueId, string controllerId, string text)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        Text = text;
    }
}

internal class PoachersJournalHandler : IHandler
{
    private readonly IMessageBroker messages;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;

    public PoachersJournalHandler(IMessageBroker messages, INetwork network, IObjectManager objects,
        IIssueOwnerResolver owners, IIssueResolver issues, IControllerIdProvider controller)
    {
        this.messages = messages;
        this.network = network;
        this.objects = objects;
        this.owners = owners;
        this.issues = issues;
        this.controller = controller;
        messages.Subscribe<NetworkPoachersJournalLog>(OnLogReceived);
    }

    public void Dispose() => messages.Unsubscribe<NetworkPoachersJournalLog>(OnLogReceived);

    public void SendLog(QuestBase quest, TextObject text)
    {
        var issue = quest.QuestGiver.Issue;
        if (ModInformation.IsClient || issue?.IssueQuest != quest ||
            !owners.TryGetOwnerControllerId(issue, out var owner) ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;

        network.SendAll(new NetworkPoachersJournalLog(giverId, issue.StringId, owner, text.ToString()));
    }

    private void OnLogReceived(MessagePayload<NetworkPoachersJournalLog> payload)
    {
        var data = payload.What;
        if (ModInformation.IsServer || data.ControllerId != controller.ControllerId) return;

        GameThread.RunSafe(() =>
        {
            if (issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) && issue.IssueQuest?.IsOngoing == true)
                issue.IssueQuest.AddLog(new TextObject(data.Text));
        }, context: nameof(PoachersJournalHandler));
    }
}
