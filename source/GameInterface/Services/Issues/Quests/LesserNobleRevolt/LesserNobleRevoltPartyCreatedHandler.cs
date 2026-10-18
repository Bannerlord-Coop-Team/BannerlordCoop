using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
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
                !(issue.IssueQuest is Quest quest) ||
                !objectManager.TryGetObjectWithLogging<MobileParty>(data.PartyId, out var party))
            {
                return;
            }

            quest._lesserNobleParty = party;
            if (!quest.IsTracked(party))
            {
                quest.AddTrackedObject(party);
            }

            if (quest._suitableVillagesToVisitList.Count > 1 &&
                !quest.IsTracked(quest._suitableVillagesToVisitList[1]))
            {
                quest.AddTrackedObject(quest._suitableVillagesToVisitList[1]);
            }
        }, context: nameof(LesserNobleRevoltPartyCreatedHandler));
    }
}
