using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network.Messages;
using GameInterface.Registry.Messages;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Messages;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>
/// Ends every registered issue being solved whose player is gone, swept at server start, on a disconnect and on a removal.
/// </summary>
internal class ActiveIssueDismisser : IHandler, IActiveIssueDismisser
{
    private static readonly ILogger Logger = LogManager.GetLogger<ActiveIssueDismisser>();

    private readonly IMessageBroker messageBroker;
    private readonly IQuestTypeRegistry registry;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IIssueEndingCoordinator endings;

    public ActiveIssueDismisser(
        IMessageBroker messageBroker,
        IQuestTypeRegistry registry,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IIssueEndingCoordinator endings)
    {
        this.messageBroker = messageBroker;
        this.registry = registry;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.endings = endings;

        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<PlayerRemoved>(Handle_PlayerRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<PlayerRemoved>(Handle_PlayerRemoved);
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        try
        {
            EndIssuesOfGonePlayers(announce: false, removedControllerId: null);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Dismissing the active issues failed");
        }
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        GameThread.RunSafe(() => EndIssuesOfGonePlayers(announce: true, removedControllerId: null), context: nameof(ActiveIssueDismisser));
    }

    // Owner entries point at a controller, so the removed player's issues would otherwise come back to the controller's next character
    private void Handle_PlayerRemoved(MessagePayload<PlayerRemoved> payload)
    {
        var controllerId = payload.What.ControllerId;
        GameThread.RunSafe(() => EndIssuesOfGonePlayers(announce: true, controllerId), context: nameof(ActiveIssueDismisser));
    }

    public void EndIssuesOfGonePlayers(bool announce, string removedControllerId)
    {
        var issueManager = Campaign.Current?.IssueManager;
        if (issueManager == null)
        {
            return;
        }

        var dismissed = 0;

        foreach (var issue in issueManager.Issues.Values.ToList()
                     .Where(issue => registry.IsRegistered(issue.GetType())))
        {
            var playerIsGone = !TryGetControllerId(issue, out var owner) || owner == removedControllerId;
            var solvedByPlayer = issue.IsSolvingWithQuest || issue.IsSolvingWithLordSolution;
            if (solvedByPlayer && !OwnerIsConnected(issue))
            {
                endings.EndWithoutOwner(issue, IssueOutcome.QuestCancel, announce);
            }
            else if (issue.IsSolvingWithAlternative && playerIsGone)
            {
                endings.EndWithoutOwner(issue, IssueOutcome.AlternativeSolution, announce);
            }
            else
            {
                continue;
            }

            dismissed++;
        }

        if (dismissed > 0 || !announce)
        {
            Logger.Information("Dismissed {count} active issues, quests do not outlive their player or the server", dismissed);
        }
    }

    private bool OwnerIsConnected(IssueBase issue)
    {
        return TryGetControllerId(issue, out var controllerId)
            && playerManager.TryGetPlayer(controllerId, out var player)
            && playerManager.IsConnected(player);
    }

    private bool TryGetControllerId(IssueBase issue, out string controllerId)
    {
        controllerId = null;

        return objectManager.TryGetId(issue.IssueOwner, out var issueOwnerId)
            && ownership.TryGetOwner(issueOwnerId, issue.StringId, out controllerId);
    }
}
