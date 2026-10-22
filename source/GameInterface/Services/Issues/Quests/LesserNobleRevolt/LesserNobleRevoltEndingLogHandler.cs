using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal sealed class LesserNobleRevoltEndingLogHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IIssueResolver issueResolver;
    private readonly IControllerIdProvider controllerIdProvider;

    public LesserNobleRevoltEndingLogHandler(
        IMessageBroker messageBroker,
        IIssueResolver issueResolver,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.issueResolver = issueResolver;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkLesserNobleRevoltEndingLog>(Handle);
    }

    public void Dispose() => messageBroker.Unsubscribe<NetworkLesserNobleRevoltEndingLog>(Handle);

    private void Handle(MessagePayload<NetworkLesserNobleRevoltEndingLog> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controllerIdProvider.ControllerId)
        {
            return;
        }

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest) || !quest.IsOngoing)
            {
                return;
            }

            // The owner's journal follows the server's line for line, so the lines past its own count are what the ending added.
            var logs = data.Logs ?? Array.Empty<TextObject>();
            for (var i = quest.JournalEntries.Count; i < logs.Length; i++)
            {
                quest.AddLog(logs[i]);
            }
        }, context: nameof(LesserNobleRevoltEndingLogHandler));
    }
}
