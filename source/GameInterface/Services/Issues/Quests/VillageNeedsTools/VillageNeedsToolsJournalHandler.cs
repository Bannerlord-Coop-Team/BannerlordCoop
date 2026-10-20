using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.Localization;
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
    private readonly IControllerIdProvider controllerIdProvider;

    public VillageNeedsToolsJournalHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, IIssueOwnershipRegistry ownership,
        IIssueResolver issueResolver, IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.ownership = ownership;
        this.issueResolver = issueResolver;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<VillageNeedsToolsJournalAdded>(Handle_Added);
        messageBroker.Subscribe<NetworkVillageNeedsToolsJournal>(Handle_Journal);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageNeedsToolsJournalAdded>(Handle_Added);
        messageBroker.Unsubscribe<NetworkVillageNeedsToolsJournal>(Handle_Journal);
    }

    private void Handle_Added(MessagePayload<VillageNeedsToolsJournalAdded> payload)
    {
        if (ModInformation.IsClient) return;

        var quest = payload.What.Quest;
        var issue = quest.QuestGiver.Issue;
        if (issue == null || !objectManager.TryGetIdWithLogging(quest.QuestGiver, out var giverId)
            || !ownership.TryGetOwner(giverId, issue.StringId, out var controllerId)) return;

        network.SendAll(new NetworkVillageNeedsToolsJournal(giverId, issue.StringId, controllerId, payload.What.Text));
    }

    private void Handle_Journal(MessagePayload<NetworkVillageNeedsToolsJournal> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (data.ControllerId != controllerIdProvider.ControllerId
                || !issueResolver.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _)
                || !(issue.IssueQuest is Quest quest) || !quest.IsOngoing) return;

            quest.AddLog(new TextObject("{=!}" + data.Text));
        }, context: nameof(VillageNeedsToolsJournalHandler));
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
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string Text;

    public NetworkVillageNeedsToolsJournal(string giverId, string issueId, string controllerId, string text)
    {
        GiverId = giverId;
        IssueId = issueId;
        ControllerId = controllerId;
        Text = text;
    }
}
