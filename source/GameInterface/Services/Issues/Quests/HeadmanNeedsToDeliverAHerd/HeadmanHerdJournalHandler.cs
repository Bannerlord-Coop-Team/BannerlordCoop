using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd;

internal class HeadmanHerdJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controllerId;

    public HeadmanHerdJournalHandler(IMessageBroker broker, IIssueResolver issues, IControllerIdProvider controllerId)
    {
        this.broker = broker;
        this.issues = issues;
        this.controllerId = controllerId;
        broker.Subscribe<NetworkQuestSolutionAccepted>(ShowGrant);
    }

    public void Dispose()
    {
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

}
