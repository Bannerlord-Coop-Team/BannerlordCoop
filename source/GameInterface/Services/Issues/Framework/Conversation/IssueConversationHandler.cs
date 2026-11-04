using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.Conversation;

internal sealed class IssueConversationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueConversationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IQuestTypeRegistry registry;
    private readonly IIssueConversationTracker tracker;
    private readonly ConversationPartyTracker partyTracker;

    public IssueConversationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IQuestTypeRegistry registry,
        IIssueConversationTracker tracker,
        ConversationPartyTracker partyTracker)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.registry = registry;
        this.tracker = tracker;
        this.partyTracker = partyTracker;

        messageBroker.Subscribe<IssueConversationOpenedLocally>(HandleOpened);
        messageBroker.Subscribe<RequestIssueConversationOpened>(HandleRequest);
        messageBroker.Subscribe<NetworkIssueConversationAllowed>(HandleAllowed);
        messageBroker.Subscribe<NetworkIssueConversationDenied>(HandleDenied);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<IssueConversationOpenedLocally>(HandleOpened);
        messageBroker.Unsubscribe<RequestIssueConversationOpened>(HandleRequest);
        messageBroker.Unsubscribe<NetworkIssueConversationAllowed>(HandleAllowed);
        messageBroker.Unsubscribe<NetworkIssueConversationDenied>(HandleDenied);
    }

    private void HandleOpened(MessagePayload<IssueConversationOpenedLocally> payload)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(payload.What.IssueOwnerId, out var giver) ||
            !IsEligible(giver.Issue)) return;

        if (ModInformation.IsServer)
            return;

        network.SendAll(new RequestIssueConversationOpened(payload.What.IssueOwnerId));
    }

    private void HandleRequest(MessagePayload<RequestIssueConversationOpened> payload)
    {
        if (ModInformation.IsClient) return;
        var peer = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (peer == null || !playerManager.TryGetPlayer(peer, out var player) ||
                !objectManager.TryGetObjectWithLogging<Hero>(payload.What.IssueOwnerId, out var giver) ||
                !TryRegister(payload.What.IssueOwnerId, player.ControllerId, giver))
            {
                if (peer != null) network.Send(peer, new NetworkIssueConversationDenied(payload.What.IssueOwnerId));
                return;
            }

            network.Send(peer, new NetworkIssueConversationAllowed(
                payload.What.IssueOwnerId, giver.Issue.StringId));
        }, context: nameof(IssueConversationHandler));
    }

    private void HandleAllowed(MessagePayload<NetworkIssueConversationAllowed> payload)
    {
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<IControllerIdProvider>(out var controller)) return;
        tracker.Register(payload.What.IssueOwnerId, controller.ControllerId, payload.What.IssueId);
    }

    private void HandleDenied(MessagePayload<NetworkIssueConversationDenied> payload)
    {
        Logger.Warning("Server denied issue conversation for {IssueOwnerId}", payload.What.IssueOwnerId);
    }

    private bool TryRegister(string issueOwnerId, string controllerId, Hero giver)
    {
        if (string.IsNullOrEmpty(controllerId) || !IsEligible(giver.Issue) ||
            string.IsNullOrEmpty(giver.Issue.StringId) ||
            !IsRequesterPresent(controllerId, giver)) return false;

        tracker.Register(issueOwnerId, controllerId, giver.Issue.StringId);
        return true;
    }

    private bool IsRequesterPresent(string controllerId, Hero giver)
    {
        if (!playerManager.TryGetPlayer(controllerId, out var player) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;

        if (giver.CurrentSettlement != null) return party.CurrentSettlement == giver.CurrentSettlement;

        var giverParty = giver.PartyBelongedTo;
        if (giverParty?.Party == null || !objectManager.TryGetId(party.Party, out var requesterPartyId)) return false;
        if (objectManager.TryGetId(giverParty.Party, out var giverPartyId) &&
            partyTracker.IsEngagerParty(giverPartyId, requesterPartyId)) return true;

        var leader = giverParty.Army?.LeaderParty;
        return leader?.AttachedParties.Contains(giverParty) == true &&
            objectManager.TryGetId(leader.Party, out var leaderPartyId) &&
            partyTracker.IsEngagerParty(leaderPartyId, requesterPartyId);
    }

    private bool IsEligible(TaleWorlds.CampaignSystem.Issues.IssueBase issue)
    {
        return issue != null && issue.IsOngoingWithoutQuest && registry.TryGet(issue.GetType(), out var descriptor) &&
            descriptor.QuestSolutionAcceptStrategy != null;
    }
}
