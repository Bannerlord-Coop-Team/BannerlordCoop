using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal sealed class LesserNobleRevoltPartyCreatedHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;
    private readonly IControllerIdProvider controllerIdProvider;

    public LesserNobleRevoltPartyCreatedHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkLesserNobleRevoltPartyCreated>(Handle);
    }

    public void Dispose() => messageBroker.Unsubscribe<NetworkLesserNobleRevoltPartyCreated>(Handle);

    private void Handle(MessagePayload<NetworkLesserNobleRevoltPartyCreated> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controllerIdProvider.ControllerId)
        {
            return;
        }

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest) || !quest.IsOngoing ||
                !objectManager.TryGetObjectWithLogging<MobileParty>(data.PartyId, out var party))
            {
                return;
            }

            using var replay = new AllowedThread();
            using var journalReplay = new QuestJournalReplay();
            quest._lesserNobleParty = party;
            if (!quest.IsTracked(party))
            {
                quest.AddTrackedObject(party);
            }

            if (quest._discreteLog == null && quest._journalEntries != null)
            {
                var taskName = new TextObject("{=Y2Z6F5Fj}Support level of {MALE_LESSER_NOBLE_NAME}");
                taskName.SetTextVariable("MALE_LESSER_NOBLE_NAME", quest._lesserNobleName);
                quest._discreteLog = quest.AddDiscreteLog(
                    quest.QuestStartedLogText,
                    taskName,
                    quest._visitedVillagesList.Count,
                    20);
            }

            if (quest._suitableVillagesToVisitList.Count > 1 &&
                !quest.IsTracked(quest._suitableVillagesToVisitList[1]))
            {
                quest.AddTrackedObject(quest._suitableVillagesToVisitList[1]);
            }
        }, context: nameof(LesserNobleRevoltPartyCreatedHandler));
    }
}
