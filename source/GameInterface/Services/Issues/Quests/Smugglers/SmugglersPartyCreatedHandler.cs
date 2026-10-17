using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace GameInterface.Services.Issues.Quests.Smugglers;

/// <summary>
/// Points the owning player's quest at the smugglers' party the server spawned for it.
/// </summary>
internal class SmugglersPartyCreatedHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;
    private readonly IControllerIdProvider controllerIdProvider;

    public SmugglersPartyCreatedHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.controllerIdProvider = controllerIdProvider;

        messageBroker.Subscribe<NetworkSmugglersPartyCreated>(Handle_NetworkSmugglersPartyCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSmugglersPartyCreated>(Handle_NetworkSmugglersPartyCreated);
    }

    private void Handle_NetworkSmugglersPartyCreated(MessagePayload<NetworkSmugglersPartyCreated> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var data = payload.What;

        // A player whose accept lost the race still has a quest until its rejection arrives
        if (data.ControllerId != controllerIdProvider.ControllerId)
        {
            return;
        }

        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest))
            {
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.PartyId, out var party))
            {
                return;
            }

            quest._smugglerParty = party;
        }, context: nameof(SmugglersPartyCreatedHandler));
    }
}
