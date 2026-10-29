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
using GameInterface.Services.UI.Notifications;
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
    private readonly IIssueConversationTracker conversationTracker;
    private readonly IIssueAcceptPreconditions acceptPreconditions;
    private readonly IQuestJournalOwnerResolver journal;
    private readonly IIssueConversationGuard conversationGuard;

    public QuestSolutionAcceptCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        IControllerIdProvider controllerIdProvider,
        IIssueConversationTracker conversationTracker,
        IIssueAcceptPreconditions acceptPreconditions,
        IQuestJournalOwnerResolver journal,
        IIssueConversationGuard conversationGuard)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;
        this.conversationTracker = conversationTracker;
        this.acceptPreconditions = acceptPreconditions;
        this.journal = journal;
        this.conversationGuard = conversationGuard;

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
            (issue.GetType().Name == "TheConquestOfSettlementIssue" &&
                !conversationTracker.TryGet(data.IssueOwnerId, player.ControllerId, data.IssueId)) ||
            !acceptPreconditions.CanTake(issue, playerHero, player.ControllerId) ||
            !ownership.TrySetOwner(data.IssueOwnerId, data.IssueId, player.ControllerId))
        {
            Reject(peer, data);
            return;
        }

        var dueTime = issue.IssueDueTime;

        try
        {
            // Creates the quest object the owner's outcomes run on, unless its accept strategy starts it, it registers no events
            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                using (new IssueDifficultyOverride(data.DifficultyMultiplier))
                {
                    issue.StartIssueWithQuest();
                }
            }

            if (issue.IssueQuest.IsTrackEnabled) issue.IssueQuest.ToggleTrackedObjects();

            descriptor.QuestSolutionAcceptStrategy.Apply(issue, data.Captured);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Starting the quest of {issue} failed", data.IssueId);
            ownership.Remove(data.IssueOwnerId);
            OfferAgain(issue, dueTime);
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

    // Undoes a half-finished accept on the server, so the refused client's reopened offer can still be taken
    private void OfferAgain(IssueBase issue, CampaignTime dueTime)
    {
        try
        {
            var strayQuest = issue.IssueQuest;

            // set to null first so cancelling the quest does not finalize the issue
            issue.IssueQuest = null;
            issue._issueState = IssueBase.IssueState.Ongoing;
            issue.IsTriedToSolveBefore = false;
            issue.IssueDueTime = dueTime;

            if (strayQuest != null && strayQuest.IsOngoing)
            {
                using (new AllowedThread())
                {
                    strayQuest.CompleteQuestWithCancel();
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Offering {issue} again after a failed accept failed", issue.StringId);
        }
        finally
        {
            journal.DiscardArchive(issue);
        }
    }

    private void Reject(NetPeer peer, RequestQuestSolutionAccept data)
    {
        long? dueTime = issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) &&
            issue.IsOngoingWithoutQuest ? issue.IssueDueTime.NumTicks : (long?)null;
        network.Send(peer, new NetworkQuestSolutionAcceptRejected(data.IssueOwnerId, data.IssueId, dueTime));
    }

    private void Handle_NetworkQuestSolutionAccepted(MessagePayload<NetworkQuestSolutionAccepted> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var data = payload.What;

        // An issue the server just made can still wait behind the giver's ended one
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out var descriptor))
            {
                return;
            }

            ownership.TrySetOwner(data.IssueOwnerId, data.IssueId, data.ControllerId);

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
        }, data.IssueOwnerId), context: nameof(QuestSolutionAcceptCoordinator));
    }

    private void Handle_NetworkQuestSolutionAcceptRejected(MessagePayload<NetworkQuestSolutionAcceptRejected> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var data = payload.What;

        // Cancelling the stray quest takes its dialog lines, which the player may still be answering
        GameThread.RunSafe(() => conversationGuard.RunWhenNotTalkingTo(() => CancelStrayQuest(data), data.IssueOwnerId),
            context: nameof(QuestSolutionAcceptCoordinator));
    }

    private void CancelStrayQuest(NetworkQuestSolutionAcceptRejected data)
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

        // This player never held the issue, so its ending must not be credited to them
        issue.IsTriedToSolveBefore = false;

        // With no winner the server still offers the issue, so undo the local accept
        // Read when this runs, which keeps message order, so a winner's accept that came first is in even if it waited
        var hasOwner = ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out _);
        if (!hasOwner)
        {
            issue._issueState = IssueBase.IssueState.Ongoing;
        }

        using (new AllowedThread())
        {
            strayQuest.CompleteQuestWithCancel();
        }

        journal.DiscardArchive(issue);
        if (data.IssueDueTimeTicks.HasValue && !hasOwner)
            issue.IssueDueTime = new CampaignTime(data.IssueDueTimeTicks.Value);
    }
}
