using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Entity;
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
    private readonly IControllerIdProvider controller;

    public FinalizationCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        IControllerIdProvider controller)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.controller = controller;

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
            if (!TryResolveOwnedIssue(data.IssueOwnerId, data.IssueId, player, out var issue, out var descriptor, out var playerHero))
            {
                return;
            }

            if (data.Outcome == IssueOutcome.QuestSuccess &&
                descriptor is IBranchOnlyFinalizationDescriptor { SuccessOnlyThroughBranchProof: true })
            {
                Logger.Warning("{issue} requires a finalization proof and cannot accept generic success", data.IssueId);
                return;
            }

            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                CompleteOnServer(issue, descriptor, data.Outcome);
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

        if (!TryGetIssueOwnerId(issue, out var issueOwnerId))
        {
            return;
        }

        ownership.Remove(issueOwnerId);

        network.SendAll(new NetworkIssueFinalized(issueOwnerId, issue.StringId, payload.What.Outcome));
    }

    private bool TryGetIssueOwnerId(IssueBase issue, out string issueOwnerId)
    {
        if (objectManager.TryGetId(issue.IssueOwner, out issueOwnerId)) return true;

        // A real giver can be removed from the server registry as part of its death before the
        // outcome event is handled. Its stamped StringId still identifies the client-side hero.
        if (!string.IsNullOrEmpty(issue.IssueOwner?.StringId))
        {
            issueOwnerId = $"Hero_{issue.IssueOwner.StringId}";
            Logger.Warning("Using the stamped issue owner id {IssueOwnerId} after registry removal", issueOwnerId);
            return true;
        }

        Logger.Error("Failed to get issue owner id for {Issue}", issue.StringId);
        issueOwnerId = null;
        return false;
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
            var isOwner = ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var owner) && owner == controller.ControllerId;
            ownership.Remove(data.IssueOwnerId);

            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _))
            {
                return;
            }

            using (new AllowedThread())
            using (new QuestEndingReplay())
            {
                Replay(issue, data.Outcome, isOwner);
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

    private void CompleteOnServer(IssueBase issue, IQuestTypeDescriptor descriptor, IssueOutcome outcome)
    {
        if (!IsQuestOutcome(outcome))
        {
            Logger.Error("{outcome} cannot be requested for {issue}", outcome, issue.StringId);
            return;
        }

        var quest = issue.IssueQuest;

        if (quest == null)
        {
            Logger.Error("{issue} has no quest to end with {outcome}", issue.StringId, outcome);
            return;
        }

        if (outcome == IssueOutcome.QuestSuccess && descriptor.FinalizationProofStrategy != null)
        {
            Logger.Warning("Ignoring generic success for {issue}; its quest requires a finalization proof", issue.StringId);
            return;
        }

        TryCompleteQuest(quest, outcome);
    }

    private static void Replay(IssueBase issue, IssueOutcome outcome, bool isOwner)
    {
        var quest = issue.IssueQuest;

        if (quest != null && !Campaign.Current.QuestManager.Quests.Contains(quest))
        {
            issue.IssueQuest = null;
            issue.IsTriedToSolveBefore = false;
            quest = null;
        }

        if (quest != null && quest.IsOngoing && TryCompleteQuest(quest, outcome))
        {
            return;
        }

        TryCompleteIssue(issue, outcome, isOwner);

        if (quest != null && quest.IsOngoing)
        {
            quest.CompleteQuestWithCancel();
        }
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

    private static bool TryCompleteIssue(IssueBase issue, IssueOutcome outcome, bool isOwner)
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
            case IssueOutcome.IssueFinishedByAiLord:
                issue.IssueFinalized();
                return true;
            case IssueOutcome.LordAcceptedCounterOffer:
                if (isOwner) issue.AddLog(new JournalLog(CampaignTime.Now, issue.LordSolutionCounterOfferAcceptLog));
                issue.IssueFinalized();
                return true;
            case IssueOutcome.LordRefusedCounterOffer:
                if (isOwner) issue.AddLog(new JournalLog(CampaignTime.Now, issue.LordSolutionCounterOfferRefuseLog));
                issue.IssueFinalized();
                return true;
            default:
                return false;
        }
    }
}
