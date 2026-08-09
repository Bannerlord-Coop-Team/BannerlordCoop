using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Handlers;

internal class AlternativeSolutionCompletionHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public AlternativeSolutionCompletionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;

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

            AlternativeSolutionCompletionRunner.CompleteOnServer(owner, issue);
        });
    }

    private bool TryResolveAuthorizedOwner(NetPeer requester, string ownerId, out Hero owner, out IssueBase issue)
    {
        owner = null;
        issue = null;

        if (requester == null || !playerManager.TryGetPlayer(requester, out var player)) return false;
        if (!objectManager.TryGetObjectWithLogging(ownerId, out owner)) return false;
        if (!IssueOwnershipRegistry.TryGetOwnerControllerId(owner, out var recordedOwner)) return false;
        if (recordedOwner != player.ControllerId) return false;
        if (owner.Issue is not IssueBase resolvedIssue) return false;
        if (!resolvedIssue.IsSolvingWithAlternative || !resolvedIssue.AlternativeSolutionReturnTimeForTroops.IsPast) return false;

        issue = resolvedIssue;
        return true;
    }
}
