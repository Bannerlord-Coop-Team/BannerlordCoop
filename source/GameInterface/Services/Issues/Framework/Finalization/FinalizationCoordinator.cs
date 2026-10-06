using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>
/// The owning player's client reports how its quest ended, the server runs that ending as the owner and
/// announces it, and then every client ends its copy of the issue without running vanilla's consequences again.
/// </summary>
internal class FinalizationCoordinator : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<FinalizationCoordinator>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;

    public FinalizationCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.ownership = ownership;
        this.playerManager = playerManager;

        messageBroker.Subscribe<QuestOutcomeRequested>(Handle_QuestOutcomeRequested);
        messageBroker.Subscribe<QuestBranchRequested>(Handle_QuestBranchRequested);
        messageBroker.Subscribe<RequestQuestOutcome>(Handle_RequestQuestOutcome);
        messageBroker.Subscribe<RequestQuestBranch>(Handle_RequestQuestBranch);
        messageBroker.Subscribe<IssueOutcomeObserved>(Handle_IssueOutcomeObserved);
        messageBroker.Subscribe<NetworkIssueFinalized>(Handle_NetworkIssueFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<QuestOutcomeRequested>(Handle_QuestOutcomeRequested);
        messageBroker.Unsubscribe<QuestBranchRequested>(Handle_QuestBranchRequested);
        messageBroker.Unsubscribe<RequestQuestOutcome>(Handle_RequestQuestOutcome);
        messageBroker.Unsubscribe<RequestQuestBranch>(Handle_RequestQuestBranch);
        messageBroker.Unsubscribe<IssueOutcomeObserved>(Handle_IssueOutcomeObserved);
        messageBroker.Unsubscribe<NetworkIssueFinalized>(Handle_NetworkIssueFinalized);
    }

    // Client only.
    private void Handle_QuestOutcomeRequested(MessagePayload<QuestOutcomeRequested> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var issue = IssueManager.GetIssueOfQuest(payload.What.Quest);
        if (issue == null || !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId))
        {
            return;
        }

        network.SendAll(new RequestQuestOutcome(issueOwnerId, issue.StringId, payload.What.Outcome));
    }

    // Client only.
    private void Handle_QuestBranchRequested(MessagePayload<QuestBranchRequested> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var issue = IssueManager.GetIssueOfQuest(payload.What.Quest);
        if (issue == null || !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId))
        {
            return;
        }

        network.SendAll(new RequestQuestBranch(issueOwnerId, issue.StringId, payload.What.Proof));
    }

    // Server only.
    private void Handle_RequestQuestOutcome(MessagePayload<RequestQuestOutcome> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        if (!TryGetPlayer(payload.Who, nameof(RequestQuestOutcome), out var player))
        {
            return;
        }

        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!TryResolveOwnedIssue(data.IssueOwnerId, data.IssueId, player, out var issue, out _, out var playerHero))
            {
                return;
            }

            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                CompleteOnServer(issue, data.Outcome);
            }
        }, context: nameof(FinalizationCoordinator));
    }

    // Server only.
    private void Handle_RequestQuestBranch(MessagePayload<RequestQuestBranch> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        if (!TryGetPlayer(payload.Who, nameof(RequestQuestBranch), out var player))
        {
            return;
        }

        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!TryResolveOwnedIssue(data.IssueOwnerId, data.IssueId, player, out var issue, out var descriptor, out var playerHero))
            {
                return;
            }

            var quest = issue.IssueQuest;
            var proofStrategy = descriptor.FinalizationProofStrategy;

            if (quest == null || proofStrategy == null)
            {
                Logger.Error("{issue} cannot run the branch {proof}", data.IssueId, data.Proof);
                return;
            }

            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                if (!proofStrategy.TryRunBranch(quest, data.Proof))
                {
                    Logger.Error("{issue} has no branch for the proof {proof}", data.IssueId, data.Proof);
                }
            }
        }, context: nameof(FinalizationCoordinator));
    }

    // Server only.
    private void Handle_IssueOutcomeObserved(MessagePayload<IssueOutcomeObserved> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        var issue = payload.What.Issue;

        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId))
        {
            return;
        }

        ownership.Remove(issueOwnerId);

        network.SendAll(new NetworkIssueFinalized(issueOwnerId, issue.StringId, payload.What.Outcome));
    }

    // Client only.
    private void Handle_NetworkIssueFinalized(MessagePayload<NetworkIssueFinalized> payload)
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

            using (new AllowedThread())
            {
                Replay(issue, data.Outcome);
            }
        }, context: nameof(FinalizationCoordinator));
    }

    private bool TryGetPlayer(object who, string messageName, out Player player)
    {
        player = null;

        if (who is NetPeer peer && playerManager.TryGetPlayer(peer, out player))
        {
            return true;
        }

        Logger.Error("Received {message} without a registered player peer", messageName);
        return false;
    }

    private bool TryResolveOwnedIssue(
        string issueOwnerId,
        string issueId,
        Player player,
        out IssueBase issue,
        out IQuestTypeDescriptor descriptor,
        out Hero playerHero)
    {
        playerHero = null;

        // Try to resolve, and if able, set the issue, descriptor and hero.
        if (!issueResolver.TryResolve(issueOwnerId, issueId, out _, out issue, out descriptor))
        {
            return false;
        }

        if (!ownership.TryGetOwner(issueOwnerId, issueId, out var ownerControllerId) || ownerControllerId != player.ControllerId)
        {
            Logger.Warning("{controller} does not own {issue}", player.ControllerId, issueId);
            return false;
        }

        return objectManager.TryGetObjectWithLogging(player.HeroId, out playerHero);
    }

    private void CompleteOnServer(IssueBase issue, IssueOutcome outcome)
    {
        if (!IsQuestOutcome(outcome))
        {
            Logger.Error("{outcome} cannot be requested for {issue}", outcome, issue.StringId);
            return;
        }

        var quest = issue.IssueQuest;

        if (quest != null)
        {
            TryCompleteQuest(quest, outcome);
            return;
        }

        // The server lost the quest object, the issue itself still has to end.
        TryCompleteIssue(issue, outcome);

        // Vanilla's timeout is announced by its own patch, the other endings are not.
        if (outcome != IssueOutcome.QuestTimeOut)
        {
            messageBroker.Publish(issue, new IssueOutcomeObserved(issue, outcome));
        }
    }

    private static void Replay(IssueBase issue, IssueOutcome outcome)
    {
        var quest = issue.IssueQuest;

        if (quest != null && quest.IsOngoing && TryCompleteQuest(quest, outcome))
        {
            return;
        }

        TryCompleteIssue(issue, outcome);
    }

    private static bool IsQuestOutcome(IssueOutcome outcome)
    {
        return outcome >= IssueOutcome.QuestSuccess && outcome <= IssueOutcome.QuestTimeOut;
    }

    private static bool TryCompleteQuest(QuestBase quest, IssueOutcome outcome)
    {
        switch (outcome)
        {
            case IssueOutcome.QuestSuccess:
                quest.CompleteQuestWithSuccess();
                return true;
            case IssueOutcome.QuestFail:
                quest.CompleteQuestWithFail();
                return true;
            case IssueOutcome.QuestBetrayal:
                quest.CompleteQuestWithBetrayal();
                return true;
            case IssueOutcome.QuestCancel:
                quest.CompleteQuestWithCancel();
                return true;
            case IssueOutcome.QuestTimeOut:
                quest.CompleteQuestWithTimeOut();
                return true;
            default:
                return false;
        }
    }

    private static bool TryCompleteIssue(IssueBase issue, IssueOutcome outcome)
    {
        switch (outcome)
        {
            case IssueOutcome.QuestSuccess:
                issue.CompleteIssueWithQuest();
                return true;
            case IssueOutcome.QuestFail:
                issue.CompleteIssueWithFail();
                return true;
            case IssueOutcome.QuestBetrayal:
                issue.CompleteIssueWithBetrayal();
                return true;
            case IssueOutcome.QuestCancel:
                issue.CompleteIssueWithCancel();
                return true;
            case IssueOutcome.QuestTimeOut:
            case IssueOutcome.IssueTimedOut:
                issue.CompleteIssueWithTimedOut();
                return true;
            case IssueOutcome.IssueStayAliveFailed:
                issue.CompleteIssueWithStayAliveConditionsFailed();
                return true;
            case IssueOutcome.AlternativeSolution:
                issue.IssueFinalized();
                return true;
            default:
                return false;
        }
    }
}
