using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPoachersJournalLog : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string Text;
    [ProtoMember(5)] public readonly int UpdateDetails;

    public NetworkPoachersJournalLog(string issueOwnerId, string issueId, string controllerId, string text, int updateDetails = -1)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        Text = text;
        UpdateDetails = updateDetails;
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
        if (issue?.IssueQuest == quest)
            SendLog(issue, text);
    }

    public void SendLog(IssueBase issue, TextObject text, int updateDetails = -1)
    {
        if (ModInformation.IsClient ||
            !owners.TryGetOwnerControllerId(issue, out var owner) ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;

        network.SendAll(new NetworkPoachersJournalLog(giverId, issue.StringId, owner, text?.ToString(), updateDetails));
    }

    private void OnLogReceived(MessagePayload<NetworkPoachersJournalLog> payload)
    {
        var data = payload.What;
        if (ModInformation.IsServer || data.ControllerId != controller.ControllerId) return;

        GameThread.RunSafe(() =>
        {
            if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _)) return;

            if (issue.IssueQuest?.IsOngoing == true)
                issue.IssueQuest.AddLog(new TextObject(data.Text));
            else if (issue.IsSolvingWithAlternative)
            {
                if (data.Text != null)
                    issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject(data.Text)));
                if (data.UpdateDetails >= 0)
                    CampaignEventDispatcher.Instance.OnIssueUpdated(issue, (IssueBase.IssueUpdateDetails)data.UpdateDetails, Hero.MainHero);
            }
        }, context: nameof(PoachersJournalHandler));
    }
}
