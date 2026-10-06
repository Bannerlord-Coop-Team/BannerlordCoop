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
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
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

    public AlternativeSolutionAcceptCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IIssueResolver issueResolver,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        IAlternativeSolutionTroopValidator troopValidator)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.issueResolver = issueResolver;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.troopValidator = troopValidator;

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
        if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero))
        {
            Reject(peer, data);
            return;
        }

        if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out var descriptor) ||
            descriptor.AlternativeSolutionAcceptStrategy == null ||
            !issue.IsOngoingWithoutQuest)
        {
            Reject(peer, data);
            return;
        }

        bool troopsAreValid;
        using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
        {
            using (new IssueDifficultyOverride(data.DifficultyMultiplier))
            {
                troopsAreValid = troopValidator.AreValid(issue, data.SentTroops);
            }
        }

        if (!troopsAreValid || !ownership.TrySetOwner(data.IssueOwnerId, data.IssueId, player.ControllerId))
        {
            Reject(peer, data);
            return;
        }

        try
        {
            troopValidator.Apply(issue, data.SentTroops);

            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                using (new IssueDifficultyOverride(data.DifficultyMultiplier))
                {
                    issue.AlternativeSolutionStartConsequence();
                    issue.StartIssueWithAlternativeSolution();
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Starting the alternative solution of {issue} failed", data.IssueId);
            ownership.Remove(data.IssueOwnerId);
            Reject(peer, data);
            return;
        }

        network.SendAll(new NetworkAlternativeSolutionAccepted(
            data.IssueOwnerId,
            data.IssueId,
            troopValidator.ToData(issue.AlternativeSolutionSentTroops),
            CaptureState(issue),
            descriptor.AlternativeSolutionAcceptStrategy.Capture(issue)));
    }

    private void Reject(NetPeer peer, RequestAlternativeSolutionAccept data)
    {
        network.Send(peer, new NetworkAlternativeSolutionAcceptRejected(data.IssueOwnerId, data.IssueId));
    }

    private void Handle_NetworkAlternativeSolutionAccepted(MessagePayload<NetworkAlternativeSolutionAccepted> payload)
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
        }, context: nameof(AlternativeSolutionAcceptCoordinator));
    }

    private void Handle_NetworkAlternativeSolutionAcceptRejected(MessagePayload<NetworkAlternativeSolutionAcceptRejected> payload)
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

            troopValidator.ReturnToMainParty(issue);
        }, context: nameof(AlternativeSolutionAcceptCoordinator));
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
