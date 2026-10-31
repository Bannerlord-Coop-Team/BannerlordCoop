using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI.Notifications;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// Sending troops for an issue's alternative solution is decided by the server. It runs the vanilla
/// start once, then every machine, the sender included, ends up with the values it produced.
/// </summary>
internal class AlternativeSolutionAcceptCoordinator : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AlternativeSolutionAcceptCoordinator>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IIssueResolver issueResolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;
    private readonly IAlternativeSolutionTroopValidator troopValidator;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IIssueAcceptPreconditions acceptPreconditions;
    private readonly IAlternativeSolutionCommitments commitments;
    private readonly IQuestJournalOwnerResolver journal;
    private readonly IIssueConversationGuard conversationGuard;

    public AlternativeSolutionAcceptCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        IAlternativeSolutionTroopValidator troopValidator,
        IControllerIdProvider controllerIdProvider,
        IIssueAcceptPreconditions acceptPreconditions,
        IAlternativeSolutionCommitments commitments,
        IQuestJournalOwnerResolver journal,
        IIssueConversationGuard conversationGuard)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.troopValidator = troopValidator;
        this.controllerIdProvider = controllerIdProvider;
        this.acceptPreconditions = acceptPreconditions;
        this.commitments = commitments;
        this.journal = journal;
        this.conversationGuard = conversationGuard;

        messageBroker.Subscribe<AlternativeSolutionAcceptRequested>(Handle_AlternativeSolutionAcceptRequested);
        messageBroker.Subscribe<RequestAlternativeSolutionAccept>(Handle_RequestAlternativeSolutionAccept);
        messageBroker.Subscribe<NetworkAlternativeSolutionAccepted>(Handle_NetworkAlternativeSolutionAccepted);
        messageBroker.Subscribe<NetworkAlternativeSolutionAcceptRejected>(Handle_NetworkAlternativeSolutionAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AlternativeSolutionAcceptRequested>(Handle_AlternativeSolutionAcceptRequested);
        messageBroker.Unsubscribe<RequestAlternativeSolutionAccept>(Handle_RequestAlternativeSolutionAccept);
        messageBroker.Unsubscribe<NetworkAlternativeSolutionAccepted>(Handle_NetworkAlternativeSolutionAccepted);
        messageBroker.Unsubscribe<NetworkAlternativeSolutionAcceptRejected>(Handle_NetworkAlternativeSolutionAcceptRejected);
    }

    private void Handle_AlternativeSolutionAcceptRequested(MessagePayload<AlternativeSolutionAcceptRequested> payload)
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

        network.SendAll(new RequestAlternativeSolutionAccept(
            issueOwnerId,
            issue.StringId,
            Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier(),
            troopValidator.ToData(issue.AlternativeSolutionSentTroops)));
    }

    private void Handle_RequestAlternativeSolutionAccept(MessagePayload<RequestAlternativeSolutionAccept> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player))
        {
            Logger.Error("Received {message} without a registered player peer", nameof(RequestAlternativeSolutionAccept));
            return;
        }

        var data = payload.What;

        GameThread.RunSafe(() => AcceptOnServer(peer, player, data), context: nameof(AlternativeSolutionAcceptCoordinator));
    }

    private void AcceptOnServer(NetPeer peer, Player player, RequestAlternativeSolutionAccept data)
    {
        data = new RequestAlternativeSolutionAccept(data.IssueOwnerId, data.IssueId, data.DifficultyMultiplier,
            commitments.Consume(player.HeroId, data.IssueOwnerId, data.IssueId, data.SentTroops));
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero))
        {
            Reject(peer, data, playerHero, "player hero missing");
            return;
        }

        if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out var descriptor) ||
            descriptor.AlternativeSolutionAcceptStrategy == null ||
            !issue.IsOngoingWithoutQuest ||
            !acceptPreconditions.CanTake(issue, playerHero, player.ControllerId))
        {
            Reject(peer, data, playerHero, "issue unavailable or preconditions refused");
            return;
        }

        bool troopsAreValid;
        bool alternativeSolutionIsAcceptable;
        using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
        {
            using (new IssueDifficultyOverride(data.DifficultyMultiplier))
            {
                alternativeSolutionIsAcceptable = descriptor.AlternativeSolutionAcceptStrategy is not IAlternativeSolutionAcceptPrecondition precondition ||
                    precondition.CanAccept(issue, playerHero);
                troopsAreValid = troopValidator.AreValid(issue, data.SentTroops);
            }
        }

        if (!alternativeSolutionIsAcceptable || !troopsAreValid || !ownership.TrySetOwner(data.IssueOwnerId, data.IssueId, player.ControllerId))
        {
            Reject(peer, data, playerHero, troopsAreValid ? "issue unavailable" : "committed troops do not satisfy the alternative solution");
            return;
        }

        AlternativeSolutionJournalEntry[] entries;
        try
        {
            troopValidator.Apply(issue, data.SentTroops);

            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                using (new IssueDifficultyOverride(data.DifficultyMultiplier))
                {
                    issue.AlternativeSolutionStartConsequence();
                    issue.StartIssueWithAlternativeSolution();
                    entries = issue.JournalEntries.Select(log => new AlternativeSolutionJournalEntry(log)).ToArray();
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Starting the alternative solution of {issue} failed", data.IssueId);
            ownership.Remove(data.IssueOwnerId);
            journal.DiscardArchive(issue);
            Reject(peer, data, playerHero, "starting the alternative solution threw");
            return;
        }

        network.SendAll(new NetworkAlternativeSolutionAccepted(
            data.IssueOwnerId,
            data.IssueId,
            troopValidator.ToData(issue.AlternativeSolutionSentTroops),
            CaptureState(issue),
            descriptor.AlternativeSolutionAcceptStrategy.Capture(issue),
            player.ControllerId));
        network.Send(peer, new NetworkAlternativeSolutionJournal(data.IssueOwnerId, data.IssueId,
            IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest, entries));
    }

    private void Reject(NetPeer peer, RequestAlternativeSolutionAccept data, Hero playerHero, string reason)
    {
        Logger.Warning("Rejected alternative accept {IssueId}: {Reason}", data.IssueId, reason);
        if (playerHero?.PartyBelongedTo != null)
        {
            troopValidator.ReturnTroopsToParty(playerHero.PartyBelongedTo, data.SentTroops);
        }

        network.Send(peer, new NetworkAlternativeSolutionAcceptRejected(data.IssueOwnerId, data.IssueId));
    }

    private void Handle_NetworkAlternativeSolutionAccepted(MessagePayload<NetworkAlternativeSolutionAccepted> payload)
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

            if (data.ControllerId != controllerIdProvider.ControllerId)
            {
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                ApplyState(issue, data.State);
                AddPlaceholderLog(issue);
                return;
            }

            // Troops first, vanilla reads the companion out of them while it starts
            troopValidator.Apply(issue, data.SentTroops);

            using (new AllowedThread())
            {
                using (new IssueDifficultyOverride(data.State.DifficultyMultiplier))
                {
                    issue.StartIssueWithAlternativeSolution();
                }
            }

            ApplyState(issue, data.State);
            descriptor.AlternativeSolutionAcceptStrategy?.Apply(issue, data.Captured);
        }, data.IssueOwnerId), context: nameof(AlternativeSolutionAcceptCoordinator));
    }

    private void Handle_NetworkAlternativeSolutionAcceptRejected(MessagePayload<NetworkAlternativeSolutionAcceptRejected> payload)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        var data = payload.What;

        // Runs at once, not behind waiting work, because a held replay would clear the sent companions first or the issue would already have moved away.
        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _))
            {
                return;
            }

            using (new AllowedThread())
            {
                troopValidator.ReturnToMainParty(issue);
            }
        }, context: nameof(AlternativeSolutionAcceptCoordinator));
    }

    // Vanilla's daily tick reads the first log entry, which only the starting machine has
    private static void AddPlaceholderLog(IssueBase issue)
    {
        if (issue.JournalEntries.Count > 0)
        {
            return;
        }

        issue._journalEntries.Add(new JournalLog(
            CampaignTime.Now,
            new TextObject(string.Empty),
            new TextObject("{=VFO7rMzK}Return Days"),
            0,
            issue.AlternativeSolutionBaseDurationInDaysInternal));
    }

    private static AlternativeSolutionVanillaState CaptureState(IssueBase issue)
    {
        return new AlternativeSolutionVanillaState(
            issue._issueDifficultyMultiplier,
            issue._failureChance,
            issue._alternativeSolutionCasualtyCount,
            issue._companionRewardSkill?.StringId ?? string.Empty,
            issue._totalTroopXpAmount,
            issue.AlternativeSolutionReturnTimeForTroops.NumTicks,
            issue.AlternativeSolutionIssueEffectClearTime.NumTicks,
            issue.IssueDueTime.NumTicks);
    }

    private static void ApplyState(IssueBase issue, AlternativeSolutionVanillaState state)
    {
        issue._issueDifficultyMultiplier = state.DifficultyMultiplier;
        issue._failureChance = state.FailureChance;
        issue._alternativeSolutionCasualtyCount = state.CasualtyCount;
        issue._companionRewardSkill = string.IsNullOrEmpty(state.CompanionRewardSkillId)
            ? null
            : MBObjectManager.Instance.GetObject<SkillObject>(state.CompanionRewardSkillId);
        issue._totalTroopXpAmount = state.TotalTroopXpAmount;
        issue.AlternativeSolutionReturnTimeForTroops = new CampaignTime(state.ReturnTimeTicks);
        issue.AlternativeSolutionIssueEffectClearTime = new CampaignTime(state.EffectClearTimeTicks);
        issue.IssueDueTime = new CampaignTime(state.IssueDueTimeTicks);
    }
}
