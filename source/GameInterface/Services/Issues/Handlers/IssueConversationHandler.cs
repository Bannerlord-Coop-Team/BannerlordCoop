using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Handlers;

internal class IssueConversationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IIssueGenerationRegistry generationRegistry;
    private readonly IIssueConversationTracker conversationTracker;

    public IssueConversationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IIssueGenerationRegistry generationRegistry,
        IIssueConversationTracker conversationTracker)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.generationRegistry = generationRegistry;
        this.conversationTracker = conversationTracker;

        messageBroker.Subscribe<IssueConversationOpenedLocally>(Handle_IssueConversationOpenedLocally);
        messageBroker.Subscribe<RequestIssueConversationOpened>(Handle_RequestIssueConversationOpened);
        messageBroker.Subscribe<NetworkIssueConversationAllowed>(Handle_NetworkIssueConversationAllowed);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<IssueConversationOpenedLocally>(Handle_IssueConversationOpenedLocally);
        messageBroker.Unsubscribe<RequestIssueConversationOpened>(Handle_RequestIssueConversationOpened);
        messageBroker.Unsubscribe<NetworkIssueConversationAllowed>(Handle_NetworkIssueConversationAllowed);
    }

    private void Handle_IssueConversationOpenedLocally(MessagePayload<IssueConversationOpenedLocally> payload)
    {
        var issueGiver = payload.What.IssueGiver;
        if (issueGiver == null || !objectManager.TryGetIdWithLogging(issueGiver, out var issueGiverId)) return;
        if (!IsMirrorEligible(issueGiver.Issue)) return;

        if (ModInformation.IsServer)
        {
            TryRegisterConversation(issueGiverId, payload.What.ControllerId);
        }
        else
        {
            network.SendAll(new RequestIssueConversationOpened(issueGiverId));
        }
    }

    private void Handle_RequestIssueConversationOpened(MessagePayload<RequestIssueConversationOpened> payload)
    {
        if (ModInformation.IsClient) return;

        var issueGiverId = payload.What.IssueGiverId;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (requester == null || !playerManager.TryGetPlayer(requester, out var player)) return;

            if (TryRegisterConversation(issueGiverId, player.ControllerId) &&
                conversationTracker.TryGetTrackedRequester(issueGiverId, player.ControllerId, out var generation))
            {
                network.Send(requester, new NetworkIssueConversationAllowed(issueGiverId, generation));
            }
            else
            {
                network.Send(requester, new NetworkIssueConversationDenied(issueGiverId));
            }
        });
    }

    private void Handle_NetworkIssueConversationAllowed(MessagePayload<NetworkIssueConversationAllowed> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return;

            conversationTracker.Register(data.IssueGiverId, controllerIdProvider.ControllerId, data.Generation);
        });
    }

    private bool TryRegisterConversation(string issueGiverId, string controllerId)
    {
        if (controllerId == null) return false;
        if (!objectManager.TryGetObjectWithLogging<Hero>(issueGiverId, out var issueGiver)) return false;
        if (issueGiver.Issue == null || !issueGiver.Issue.IsOngoingWithoutQuest || !issueGiver.Issue.IssueStayAliveConditions()) return false;
        if (!IsMirrorEligible(issueGiver.Issue)) return false;

        generationRegistry.TryGetGeneration(issueGiver, out var generation);
        conversationTracker.Register(issueGiverId, controllerId, generation);
        return true;
    }

    private static bool IsMirrorEligible(TaleWorlds.CampaignSystem.Issues.IssueBase issue)
    {
        if (issue == null) return false;

        var descriptor = QuestTypeRegistry.Get(issue);
        return descriptor != null && (descriptor.SupportsQuestSolutionAccept || descriptor.SupportsAlternativeAccept);
    }
}
