using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Registry.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Notifications;
using LiteNetLib;
using System;
using System.Collections.Generic;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>The server accepts a decree and applies its counter-offer as the owning player.</summary>
internal class LordSolutionCoordinator : ILordSolutionCoordinator, IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordSolutionCoordinator>();
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver resolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IIssueAcceptPreconditions preconditions;
    private readonly IControllerIdProvider controller;
    private readonly IQuestJournalOwnerResolver journal;
    private readonly HashSet<IssueBase> pendingDecisions = new HashSet<IssueBase>();

    public LordSolutionCoordinator(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueResolver resolver, IIssueOwnershipRegistry ownership, IPlayerManager players,
        IIssueAcceptPreconditions preconditions, IControllerIdProvider controller, IQuestJournalOwnerResolver journal)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.resolver = resolver;
        this.ownership = ownership;
        this.players = players;
        this.preconditions = preconditions;
        this.controller = controller;
        this.journal = journal;
        broker.Subscribe<RequestLordSolution>(HandleRequest);
        broker.Subscribe<NetworkLordSolutionStarted>(HandleStarted);
        broker.Subscribe<NetworkLordSolutionRejected>(HandleRejected);
        broker.Subscribe<NetworkIssueFinalized>(HandleFinalized);
        broker.Subscribe<AllGameObjectsRegistered>(HandleSessionLoaded);
    }

    public void Dispose()
    {
        broker.Unsubscribe<RequestLordSolution>(HandleRequest);
        broker.Unsubscribe<NetworkLordSolutionStarted>(HandleStarted);
        broker.Unsubscribe<NetworkLordSolutionRejected>(HandleRejected);
        broker.Unsubscribe<NetworkIssueFinalized>(HandleFinalized);
        broker.Unsubscribe<AllGameObjectsRegistered>(HandleSessionLoaded);
    }

    public bool Before(IssueBase issue, LordSolutionOperation operation)
    {
        if (ModInformation.IsServer) return true;
        if (operation == LordSolutionOperation.Start && CallOriginalPolicy.IsOriginalAllowed()) return true;
        // A menu replay may reach a no-counteroffer ending, but it must still ask the server to pay it.
        if (operation != LordSolutionOperation.Start &&
            (pendingDecisions.Contains(issue) || !objects.TryGetId(issue.IssueOwner, out var ownerId) ||
                !ownership.TryGetOwner(ownerId, issue.StringId, out var owner) || owner != controller.ControllerId)) return false;
        if (objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId))
        {
            // Vanilla decides when the merchant conversation ends, then reopens the menu before the server replies.
            if (operation != LordSolutionOperation.Start) pendingDecisions.Add(issue);
            network.SendAll(new RequestLordSolution(giverId, issue.StringId, operation,
                Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier()));
        }
        return false;
    }

    public bool IsDecisionPending(IssueBase issue) => pendingDecisions.Contains(issue);

    private void HandleFinalized(MessagePayload<NetworkIssueFinalized> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() => ClearPending(payload.What.IssueOwnerId, payload.What.IssueId),
            context: nameof(LordSolutionCoordinator));
    }

    private void HandleSessionLoaded(MessagePayload<AllGameObjectsRegistered> payload) =>
        GameThread.RunSafe(pendingDecisions.Clear, context: nameof(LordSolutionCoordinator));

    private void HandleRequest(MessagePayload<RequestLordSolution> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer) || !players.TryGetPlayer(peer, out var player)) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!resolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !issue.IsThereLordSolution || !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
                hero.PartyBelongedTo == null)
            {
                Reject(peer, data);
                return;
            }

            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            using (new IssueDifficultyOverride(data.Operation == LordSolutionOperation.Start ? data.Difficulty : issue._issueDifficultyMultiplier))
            {
                if (data.Operation == LordSolutionOperation.Start)
                {
                    if (hero.PartyBelongedTo.CurrentSettlement != issue.IssueOwner.CurrentSettlement ||
                        issue.IssueOwner.CurrentSettlement?.OwnerClan != hero.Clan || hero.Clan == null ||
                        !issue.IsOngoingWithoutQuest || !preconditions.CanTake(issue, hero, player.ControllerId) ||
                        !issue.LordSolutionCondition(out _) || Clan.PlayerClan.Influence < issue.NeededInfluenceForLordSolution ||
                        !ownership.TrySetOwner(data.IssueOwnerId, data.IssueId, player.ControllerId))
                    {
                        Reject(peer, data);
                        return;
                    }

                    issue.StartIssueWithLordSolution();
                    network.SendAll(new NetworkLordSolutionStarted(data.IssueOwnerId, data.IssueId,
                        player.ControllerId, issue._issueDifficultyMultiplier));
                    return;
                }

                if (!issue.IsSolvingWithLordSolution ||
                    !ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var owner) || owner != player.ControllerId)
                {
                    Reject(peer, data);
                    return;
                }

                try
                {
                    IssueOutcome outcome;
                    switch (data.Operation)
                    {
                        case LordSolutionOperation.AcceptCounterOffer:
                            issue.CompleteIssueWithLordSolutionWithAcceptCounterOffer();
                            outcome = IssueOutcome.LordAcceptedCounterOffer;
                            break;
                        case LordSolutionOperation.RefuseCounterOffer:
                            if (issue.CounterOfferHero != null && Clan.PlayerClan.Influence < issue.NeededInfluenceForLordSolution)
                            {
                                Reject(peer, data);
                                return;
                            }
                            issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer();
                            outcome = IssueOutcome.LordRefusedCounterOffer;
                            break;
                        default:
                            Reject(peer, data);
                            return;
                    }
                    broker.Publish(issue, new IssueOutcomeObserved(issue, outcome));
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Applying the lord solution of {issue} failed", data.IssueId);
                    Reject(peer, data);
                }
            }
        }, context: nameof(LordSolutionCoordinator));
    }

    private void Reject(NetPeer peer, RequestLordSolution data)
    {
        var message = data.Operation == LordSolutionOperation.Start
            ? "The lord solution could not be accepted. Check your influence and speak to the issue giver again."
            : "The lord solution decision could not be applied. Check your influence and try again.";
        network.Send(peer, new SendInformationMessage(message));
        if (data.Operation != LordSolutionOperation.Start)
            network.Send(peer, new NetworkLordSolutionRejected(data.IssueOwnerId, data.IssueId));
    }

    private void HandleRejected(MessagePayload<NetworkLordSolutionRejected> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() => ClearPending(data.IssueOwnerId, data.IssueId),
            context: nameof(LordSolutionCoordinator));
    }

    private void ClearPending(string issueOwnerId, string issueId)
    {
        if (!objects.TryGetObject<Hero>(issueOwnerId, out var owner)) return;
        pendingDecisions.RemoveWhere(issue => issue.StringId == issueId && issue.IssueOwner == owner);
    }

    private void HandleStarted(MessagePayload<NetworkLordSolutionStarted> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!resolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _)) return;
            ownership.TrySetOwner(data.IssueOwnerId, data.IssueId, data.ControllerId);
            var strayQuest = issue.IssueQuest;
            issue.IssueQuest = null;
            using (new AllowedThread())
            using (new IssueDifficultyOverride(data.Difficulty))
            {
                if (strayQuest != null)
                {
                    if (strayQuest.IsOngoing) strayQuest.CompleteQuestWithCancel();
                    journal.DiscardArchive(issue);
                }
                if (data.ControllerId == controller.ControllerId) issue.StartIssueWithLordSolution();
                else issue._issueState = IssueBase.IssueState.SolvingWithLordSolution;
            }
            issue._issueDifficultyMultiplier = data.Difficulty;
            issue.IsTriedToSolveBefore = data.ControllerId == controller.ControllerId;
            pendingDecisions.Remove(issue);
            // The conversation's menu event may have passed while acceptance waited for the server. While the
            // artisan is still talking, the menu event after that conversation brings the merchant, as in vanilla.
            if (data.ControllerId == controller.ControllerId && Campaign.Current.ConversationManager?.IsConversationInProgress != true)
                issue.BeforeGameMenuOpened(null);
        }, context: nameof(LordSolutionCoordinator));
    }
}
