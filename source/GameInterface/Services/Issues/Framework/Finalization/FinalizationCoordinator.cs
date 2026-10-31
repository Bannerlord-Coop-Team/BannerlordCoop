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
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

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
    private readonly IAlternativeSolutionCommitments commitments;
    private readonly IIssueConversationGuard conversationGuard;
    private readonly IQuestSolutionAcceptCoordinator acceptCoordinator;

    public FinalizationCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        IControllerIdProvider controller,
        IAlternativeSolutionCommitments commitments,
        IIssueConversationGuard conversationGuard,
        IQuestSolutionAcceptCoordinator acceptCoordinator)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.controller = controller;
        this.commitments = commitments;
        this.conversationGuard = conversationGuard;
        this.acceptCoordinator = acceptCoordinator;

        messageBroker.Subscribe<QuestOutcomeRequested>(Handle_QuestOutcomeRequested);
        messageBroker.Subscribe<QuestBranchRequested>(Handle_QuestBranchRequested);
        messageBroker.Subscribe<RequestQuestOutcome>(Handle_RequestQuestOutcome);
        messageBroker.Subscribe<RequestQuestBranch>(Handle_RequestQuestBranch);
        messageBroker.Subscribe<IssueOutcomeObserved>(Handle_IssueOutcomeObserved);
        messageBroker.Subscribe<IssueJournalObserved>(Handle_IssueJournalObserved);
        messageBroker.Subscribe<NetworkIssueJournal>(Handle_NetworkIssueJournal);
        messageBroker.Subscribe<NetworkIssueFinalized>(Handle_NetworkIssueFinalized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<QuestOutcomeRequested>(Handle_QuestOutcomeRequested);
        messageBroker.Unsubscribe<QuestBranchRequested>(Handle_QuestBranchRequested);
        messageBroker.Unsubscribe<RequestQuestOutcome>(Handle_RequestQuestOutcome);
        messageBroker.Unsubscribe<RequestQuestBranch>(Handle_RequestQuestBranch);
        messageBroker.Unsubscribe<IssueOutcomeObserved>(Handle_IssueOutcomeObserved);
        messageBroker.Unsubscribe<IssueJournalObserved>(Handle_IssueJournalObserved);
        messageBroker.Unsubscribe<NetworkIssueJournal>(Handle_NetworkIssueJournal);
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

        acceptCoordinator.RunWhenAccepted(issue, () =>
            network.SendAll(new RequestQuestOutcome(issueOwnerId, issue.StringId, payload.What.Outcome, payload.What.Log)));
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

        acceptCoordinator.RunWhenAccepted(issue, () =>
            network.SendAll(new RequestQuestBranch(issueOwnerId, issue.StringId, payload.What.Proof)));
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

            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                CompleteOnServer(issue, descriptor, data.Outcome, data.Log);
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

    private void Handle_IssueJournalObserved(MessagePayload<IssueJournalObserved> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (issue.IsSolvingWithAlternative ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var giverId) ||
            !ownership.TryGetOwner(giverId, issue.StringId, out var owner) ||
            !playerManager.TryGetPeer(owner, out var peer)) return;
        if (issue.IssueQuest != null && issueResolver.TryResolve(giverId, issue.StringId,
            out _, out _, out var descriptor) && descriptor is ISuppressEndingJournalSnapshotDescriptor)
            return;

        var quest = issue.IssueQuest;
        var entries = quest?.JournalEntries ?? issue.JournalEntries;
        network.Send(peer, new NetworkIssueJournal(giverId, issue.StringId, quest != null,
            entries.Where(log => log.LogText != null).Select(log => new IssueJournalEntry(log)).ToArray()));
    }

    private void Handle_NetworkIssueJournal(MessagePayload<NetworkIssueJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var owner) || owner != controller.ControllerId ||
                !issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out var descriptor)) return;

            var quest = issue.IssueQuest;
            if (data.IsQuestJournal && (quest == null || !quest.IsOngoing ||
                !Campaign.Current.QuestManager.Quests.Contains(quest))) return;
            if (!data.IsQuestJournal && (quest != null || issue.IsSolvingWithAlternative)) return;

            using (new AllowedThread())
            using (new QuestJournalReplay())
            {
                if (quest != null)
                {
                    var logs = data.Logs ?? Array.Empty<IssueJournalEntry>();
                    if (descriptor is ISpecializedEndingJournalDescriptor)
                    {
                        foreach (var log in logs)
                        {
                            var matches = quest.JournalEntries.Where(existing =>
                                existing.Type == log.Type && existing.Range == log.Range &&
                                SameText(existing.LogText, log.Text) && SameText(existing.TaskName, log.TaskName)).ToArray();
                            if (matches.Length == 0)
                                matches = quest.JournalEntries.Where(existing =>
                                    existing.Type == log.Type && existing.Range == log.Range &&
                                    SameKey(existing.LogText, log.Text) && SameKey(existing.TaskName, log.TaskName)).ToArray();
                            var existing = matches.FirstOrDefault(entry => entry.LogTime.NumTicks == log.Time.NumTicks);
                            if (existing == null && matches.Length == 1) existing = matches[0];
                            if (existing == null && matches.Length > 0)
                            {
                                Logger.Warning("Ambiguous specialized journal row for {issue}; using journal order", data.IssueId);
                                existing = matches[0];
                            }
                            if (existing != null)
                            {
                                existing.UpdateCurrentProgress(Math.Max(existing.CurrentProgress, log.Progress));
                            }
                            else if (matches.Length == 0)
                            {
                                quest._journalEntries.Add(log.ToJournalLog());
                            }
                        }
                    }
                    else
                    {
                        quest._journalEntries.Clear();
                        foreach (var log in logs)
                            quest._journalEntries.Add(log.ToJournalLog());
                    }
                    CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
                }
                else
                {
                    var existing = issue.JournalEntries.Where(log => log.LogText != null)
                        .GroupBy(log => log.LogText.ToString()).ToDictionary(group => group.Key, group => group.Count());
                    foreach (var log in data.Logs ?? Array.Empty<IssueJournalEntry>())
                    {
                        if (log.Text == null) continue;
                        var key = log.Text.ToString();
                        if (existing.TryGetValue(key, out var count) && count > 0) existing[key] = count - 1;
                        else issue.AddLog(log.ToJournalLog());
                    }
                }
            }
        }, context: nameof(FinalizationCoordinator));
    }

    private static bool SameText(TextObject first, TextObject second)
    {
        if (ReferenceEquals(first, second)) return true;
        if (first == null || second == null || first.Value != second.Value ||
            (first.Attributes == null) != (second.Attributes == null) ||
            (first.Attributes?.Count ?? 0) != (second.Attributes?.Count ?? 0)) return false;
        if (first.Attributes == null) return true;
        foreach (var pair in first.Attributes)
        {
            if (second.Attributes == null || !second.Attributes.TryGetValue(pair.Key, out var value)) return false;
            if (pair.Value is TextObject text)
            {
                if (!(value is TextObject other) || !SameText(text, other)) return false;
            }
            else if (!Equals(pair.Value, value)) return false;
        }
        return true;
    }

    private static bool SameKey(TextObject first, TextObject second) =>
        ReferenceEquals(first, second) || (first != null && second != null && first.Value == second.Value);

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

        commitments.ReturnForIssue(issueOwnerId, issue.StringId);
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
            // The owner's running quest ends mid dialog as in vanilla. Anything else waits, because the lines on screen read the issue or its quest
            if (IsOwnersRunningQuest(data))
            {
                conversationGuard.RunInOrder(() => ReplayEnding(data), data.IssueOwnerId);
            }
            else
            {
                conversationGuard.RunWhenNotTalkingTo(() => ReplayEnding(data), data.IssueOwnerId);
            }
        }, context: nameof(FinalizationCoordinator));
    }

    private void ReplayEnding(NetworkIssueFinalized data)
    {
        var hasOwner = ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var owner);
        var isOwner = hasOwner && owner == controller.ControllerId;
        if (hasOwner) ownership.Remove(data.IssueOwnerId);

        if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _))
        {
            return;
        }

        using (new AllowedThread())
        using (new QuestEndingReplay())
        using (new QuestJournalReplay())
        {
            Replay(issue, data.Outcome, isOwner);
        }
    }

    // The quest Replay drives, a hand in for example, rather than one it would only discard
    private bool IsOwnersRunningQuest(NetworkIssueFinalized data)
    {
        if (!ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var owner) || owner != controller.ControllerId ||
            !objectManager.TryGetObject<Hero>(data.IssueOwnerId, out var issueOwner) || issueOwner.Issue?.StringId != data.IssueId)
        {
            return false;
        }

        var quest = issueOwner.Issue.IssueQuest;
        return quest != null && Campaign.Current.QuestManager.Quests.Contains(quest);
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

    private void CompleteOnServer(IssueBase issue, IQuestTypeDescriptor descriptor, IssueOutcome outcome, TextObject log)
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

        TryCompleteQuest(quest, outcome, log);
    }

    private static void Replay(IssueBase issue, IssueOutcome outcome, bool isOwner)
    {
        var quest = issue.IssueQuest;

        if (quest != null && (!isOwner || !Campaign.Current.QuestManager.Quests.Contains(quest)))
        {
            // Discard mirror bookkeeping without running task cancellation or quest gameplay hooks.
            quest.ClearRelatedFields();
            quest._questState = QuestBase.QuestStates.Finalized;
            quest.OfferDialogFlow = null;
            quest.DiscussDialogFlow = null;
            quest.QuestCharacterDialogFlow = null;
            quest.RemoveAllTrackedObjects();
            if (!string.IsNullOrEmpty(quest.StringId) &&
                !Campaign.Current.QuestManager.Quests.Any(other => other != quest && other.StringId == quest.StringId))
                quest.RemoveAllMapMarkers();
            Campaign.Current.QuestManager.OnQuestFinalized(quest);
            issue.IssueQuest = null;
            issue.IsTriedToSolveBefore = false;
            quest = null;
        }

        if (!isOwner && quest == null)
        {
            issue.IsTriedToSolveBefore = false;
            issue.IssueFinalized();
            return;
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

    private static bool TryCompleteQuest(QuestBase quest, IssueOutcome outcome, TextObject log = null)
    {
        switch (outcome)
        {
            case IssueOutcome.QuestSuccess:
                quest.CompleteQuestWithSuccess();
                return true;
            case IssueOutcome.QuestFail:
                quest.CompleteQuestWithFail(log);
                return true;
            case IssueOutcome.QuestBetrayal:
                quest.CompleteQuestWithBetrayal(log);
                return true;
            case IssueOutcome.QuestCancel:
                quest.CompleteQuestWithCancel(log);
                return true;
            case IssueOutcome.QuestTimeOut:
                quest.CompleteQuestWithTimeOut(log);
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
                issue.IssueFinalized();
                if (isOwner) CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.IssueFinishedWithBetrayal, Hero.MainHero);
                return true;
            case IssueOutcome.LordRefusedCounterOffer:
                issue.IssueFinalized();
                if (isOwner) CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess, Hero.MainHero);
                return true;
            default:
                return false;
        }
    }
}
