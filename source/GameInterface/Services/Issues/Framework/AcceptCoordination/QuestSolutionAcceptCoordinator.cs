using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// The first player to accept an issue's quest solution gets it. The server arbitrates and keeps a
/// quest object for the owner's outcomes to run on, other clients only learn that the issue is taken.
/// </summary>
internal class QuestSolutionAcceptCoordinator : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<QuestSolutionAcceptCoordinator>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;

    public QuestSolutionAcceptCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;

        messageBroker.Subscribe<QuestSolutionAcceptTriggered>(Handle_QuestSolutionAcceptTriggered);
        messageBroker.Subscribe<RequestQuestSolutionAccept>(Handle_RequestQuestSolutionAccept);
        messageBroker.Subscribe<NetworkQuestSolutionAccepted>(Handle_NetworkQuestSolutionAccepted);
        messageBroker.Subscribe<NetworkQuestSolutionAcceptRejected>(Handle_NetworkQuestSolutionAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<QuestSolutionAcceptTriggered>(Handle_QuestSolutionAcceptTriggered);
        messageBroker.Unsubscribe<RequestQuestSolutionAccept>(Handle_RequestQuestSolutionAccept);
        messageBroker.Unsubscribe<NetworkQuestSolutionAccepted>(Handle_NetworkQuestSolutionAccepted);
        messageBroker.Unsubscribe<NetworkQuestSolutionAcceptRejected>(Handle_NetworkQuestSolutionAcceptRejected);
    }

    private void Handle_QuestSolutionAcceptTriggered(MessagePayload<QuestSolutionAcceptTriggered> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var issue = payload.What.Issue;

        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId))
        {
            return;
        }

        if (!issueResolver.TryResolve(issueOwnerId, issue.StringId, out _, out _, out var descriptor))
        {
            return;
        }

        var captured = descriptor.QuestSolutionAcceptStrategy.Capture(issue);

        network.SendAll(new RequestQuestSolutionAccept(issueOwnerId, issue.StringId, issue._issueDifficultyMultiplier, captured));
    }

    private void Handle_RequestQuestSolutionAccept(MessagePayload<RequestQuestSolutionAccept> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {message} without a registered player peer", nameof(RequestQuestSolutionAccept));
            return;
        }

        var data = payload.What;

        GameThread.RunSafe(() => AcceptOnServer(peer, player, data), context: nameof(QuestSolutionAcceptCoordinator));
    }

    private void AcceptOnServer(NetPeer peer, Player player, RequestQuestSolutionAccept data)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero))
        {
            Reject(peer, data);
            return;
        }

        if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out var descriptor) ||
            !issue.IsOngoingWithoutQuest ||
            !ownership.TrySetOwner(data.IssueOwnerId, data.IssueId, player.ControllerId))
        {
            Reject(peer, data);
            return;
        }

        try
        {
            // Creates the quest object the owner's outcomes run on, it is never started so it registers no events
            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                using (new IssueDifficultyOverride(data.DifficultyMultiplier))
                {
                    issue.StartIssueWithQuest();
                }
            }

            descriptor.QuestSolutionAcceptStrategy.Apply(issue, data.Captured);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Starting the quest of {issue} failed", data.IssueId);
            ownership.Remove(data.IssueOwnerId);
            Reject(peer, data);
            return;
        }

        network.SendAll(new NetworkQuestSolutionAccepted(
            data.IssueOwnerId,
            data.IssueId,
            player.ControllerId,
            data.DifficultyMultiplier,
            data.Captured));
    }

    private void Reject(NetPeer peer, RequestQuestSolutionAccept data)
    {
        network.Send(peer, new NetworkQuestSolutionAcceptRejected(data.IssueOwnerId, data.IssueId));
    }

    private void Handle_NetworkQuestSolutionAccepted(MessagePayload<NetworkQuestSolutionAccepted> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out var descriptor))
            {
                return;
            }

            // The accepting player already ran the accept locally
            if (data.ControllerId == controllerIdProvider.ControllerId)
            {
                return;
            }

            // No quest object here, its dialogs would answer this player's conversations too
            issue._issueDifficultyMultiplier = data.DifficultyMultiplier;
            issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            issue.IssueDueTime = CampaignTime.Never;

            descriptor.QuestSolutionAcceptStrategy.Apply(issue, data.Captured);
        }, context: nameof(QuestSolutionAcceptCoordinator));
    }

    private void Handle_NetworkQuestSolutionAcceptRejected(MessagePayload<NetworkQuestSolutionAcceptRejected> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _))
            {
                return;
            }

            var strayQuest = issue.IssueQuest;
            if (strayQuest == null)
            {
                return;
            }

            // set to null first so cancelling the quest does not finalize the issue the winner owns
            issue.IssueQuest = null;

            using (new AllowedThread())
            {
                strayQuest.CompleteQuestWithCancel();
            }
        }, context: nameof(QuestSolutionAcceptCoordinator));
    }
}
