using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal sealed class LesserNobleRevoltJournalUpdateHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;
    private readonly IControllerIdProvider controllerIdProvider;

    public LesserNobleRevoltJournalUpdateHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkLesserNobleRevoltJournalUpdate>(Handle);
    }

    public void Dispose() => messageBroker.Unsubscribe<NetworkLesserNobleRevoltJournalUpdate>(Handle);

    private void Handle(MessagePayload<NetworkLesserNobleRevoltJournalUpdate> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controllerIdProvider.ControllerId)
        {
            return;
        }

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest) ||
                !objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement))
            {
                return;
            }

            if (!data.Notification)
            {
                LesserNobleRevoltWorldEventPatches.ApplyClientSettlementProgress(quest, settlement, data.VisitCount);
            }
            else if (LesserNobleRevoltWorldEventPatches.ShouldApplyClientNotification(quest, data.VisitCount))
            {
                LesserNobleRevoltWorldEventPatches.ApplyClientHourlyJournalNotification(quest, settlement);
            }
        }, context: nameof(LesserNobleRevoltJournalUpdateHandler));
    }
}
