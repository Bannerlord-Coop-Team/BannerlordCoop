using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal sealed class LadysKnightOutJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;

    public LadysKnightOutJournalHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueOwnerResolver owners, IIssueResolver issues, IControllerIdProvider controller)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.owners = owners;
        this.issues = issues;
        this.controller = controller;
        broker.Subscribe<LadysKnightOutLogAdded>(HandleAdded);
        broker.Subscribe<LadysKnightOutIssueLogAdded>(HandleIssueAdded);
        broker.Subscribe<NetworkLadysKnightOutLog>(HandleLog);
    }

    public void Dispose()
    {
        broker.Unsubscribe<LadysKnightOutLogAdded>(HandleAdded);
        broker.Unsubscribe<LadysKnightOutIssueLogAdded>(HandleIssueAdded);
        broker.Unsubscribe<NetworkLadysKnightOutLog>(HandleLog);
    }

    private void HandleIssueAdded(MessagePayload<LadysKnightOutIssueLogAdded> payload)
    {
        var issue = payload.What.Issue;
        if (ModInformation.IsClient || !owners.TryGetOwnerControllerId(issue, out var owner) ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        network.SendAll(new NetworkLadysKnightOutLog(
            giverId,
            issue.StringId,
            owner,
            issue.JournalEntries[issue.JournalEntries.Count - 1].LogText.ToString(),
            true));
    }

    private void HandleAdded(MessagePayload<LadysKnightOutLogAdded> payload)
    {
        if (ModInformation.IsClient) return;
        var quest = payload.What.Quest;
        var issue = quest.QuestGiver.Issue;
        if (issue == null || !owners.TryGetOwnerControllerId(issue, out var owner) ||
            !objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId)) return;
        network.SendAll(new NetworkLadysKnightOutLog(
            giverId,
            issue.StringId,
            owner,
            payload.What.Log.LogText.ToString(),
            false));
    }

    private void HandleLog(MessagePayload<NetworkLadysKnightOutLog> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (data.ControllerId != controller.ControllerId ||
                !issues.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _)) return;
            using (new AllowedThread())
            {
                if (data.IsIssueLog) issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject(data.Text)));
                else if (issue.IssueQuest is Quest quest) quest.AddLog(new TextObject(data.Text));
            }
        }, context: nameof(LadysKnightOutJournalHandler));
    }
}
