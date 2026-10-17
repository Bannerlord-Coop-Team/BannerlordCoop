using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHeadmanNeedsGrainLog : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string Text;
    [ProtoMember(4)] public readonly bool IsQuestLog;

    public NetworkHeadmanNeedsGrainLog(string giverId, string issueId, string text, bool isQuestLog)
    {
        GiverId = giverId;
        IssueId = issueId;
        Text = text;
        IsQuestLog = isQuestLog;
    }
}

internal class HeadmanNeedsGrainJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IControllerIdProvider controller;

    public HeadmanNeedsGrainJournalHandler(IMessageBroker broker, IObjectManager objectManager,
        IIssueOwnershipRegistry ownership, IControllerIdProvider controller)
    {
        this.broker = broker;
        this.objectManager = objectManager;
        this.ownership = ownership;
        this.controller = controller;
        broker.Subscribe<NetworkHeadmanNeedsGrainLog>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<NetworkHeadmanNeedsGrainLog>(Handle);

    private void Handle(MessagePayload<NetworkHeadmanNeedsGrainLog> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!ownership.TryGetOwner(data.GiverId, data.IssueId, out var owner) || owner != controller.ControllerId) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            var issue = giver.Issue;
            if (issue == null || issue.StringId != data.IssueId) return;
            var text = new TextObject("{=!}" + data.Text);
            if (data.IsQuestLog) issue.IssueQuest?.AddLog(text);
            else issue.AddLog(new JournalLog(CampaignTime.Now, text));
        }, context: nameof(HeadmanNeedsGrainJournalHandler));
    }
}
