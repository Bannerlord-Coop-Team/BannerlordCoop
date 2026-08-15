using Common;
using Common.Logging;
using Common.Messaging;
using System;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Handlers;

internal class AlternativeSolutionCompletionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AlternativeSolutionCompletionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IIssueOwnershipRegistry ownershipRegistry;

    public AlternativeSolutionCompletionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IIssueOwnershipRegistry ownershipRegistry)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.ownershipRegistry = ownershipRegistry;

        messageBroker.Subscribe<RequestAlternativeSolutionCompletion>(Handle_RequestAlternativeSolutionCompletion);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestAlternativeSolutionCompletion>(Handle_RequestAlternativeSolutionCompletion);
    }

    private void Handle_RequestAlternativeSolutionCompletion(MessagePayload<RequestAlternativeSolutionCompletion> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!TryResolveAuthorizedOwner(requester, ownerId, out var owner, out var issue)) return;

            try
            {
                AlternativeSolutionCompletionRunner.CompleteOnServer(owner, issue);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to complete {Message} for owner {Owner}", nameof(RequestAlternativeSolutionCompletion), ownerId);
            }
        });
    }

    private bool TryResolveAuthorizedOwner(NetPeer requester, string ownerId, out Hero owner, out IssueBase issue)
    {
        owner = null;
        issue = null;

        if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
        {
            Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                nameof(RequestAlternativeSolutionCompletion), ownerId);
            return false;
        }

        if (!objectManager.TryGetObjectWithLogging(ownerId, out owner)) return false;

        if (!ownershipRegistry.TryGetOwnerControllerId(owner, out var recordedOwner))
        {
            Logger.Error("Rejecting {Message} from {Requester} - no recorded owner for {Owner}",
                nameof(RequestAlternativeSolutionCompletion), player.ControllerId, ownerId);
            return false;
        }

        if (recordedOwner != player.ControllerId)
        {
            Logger.Error("Rejecting {Message} from {Requester}, who is not the recorded owner of {Owner}",
                nameof(RequestAlternativeSolutionCompletion), player.ControllerId, ownerId);
            return false;
        }

        if (owner.Issue is not IssueBase resolvedIssue)
        {
            Logger.Error("Rejecting {Message} for owner {Owner} - no active issue",
                nameof(RequestAlternativeSolutionCompletion), ownerId);
            return false;
        }

        if (!resolvedIssue.IsSolvingWithAlternative || !resolvedIssue.AlternativeSolutionReturnTimeForTroops.IsPast)
        {
            Logger.Error("Rejecting {Message} for owner {Owner} - issue is not a due alternative-solution completion",
                nameof(RequestAlternativeSolutionCompletion), ownerId);
            return false;
        }

        issue = resolvedIssue;
        return true;
    }
}
