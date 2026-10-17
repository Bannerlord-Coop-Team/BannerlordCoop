using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network.Messages;
using GameInterface.Registry.Messages;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>
/// When the server starts and whenever a player leaves,every registered issue being solved whose
/// player is gone is ended silently, so its notable does not stay stuck on it. For this first
/// iteration of quests, this will be the behavior, but this will change in the future.
/// </summary>
internal class ActiveIssueDismisser : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ActiveIssueDismisser>();

    private readonly IMessageBroker messageBroker;
    private readonly IQuestTypeRegistry registry;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;

    public ActiveIssueDismisser(
        IMessageBroker messageBroker,
        IQuestTypeRegistry registry,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.registry = registry;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.objectManager = objectManager;

        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    private void Handle_AllGameObjectsRegistered(MessagePayload<AllGameObjectsRegistered> payload)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        try
        {
            Dismiss(announce: false);
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

        GameThread.RunSafe(() => Dismiss(announce: true), context: nameof(ActiveIssueDismisser));
    }

    private void Dismiss(bool announce)
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
            try
            {
                if (issue.IsSolvingWithQuest && !OwnerIsConnected(issue))
                {
                    DismissQuest(issue, announce);
                    dismissed++;
                }
                else if (issue.IsSolvingWithAlternative && !HasOwner(issue))
                {
                    DismissTroops(issue, announce);
                    dismissed++;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Dismissing {issue} failed", issue.StringId);
            }
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

    private bool HasOwner(IssueBase issue)
    {
        return TryGetControllerId(issue, out _);
    }

    private bool TryGetControllerId(IssueBase issue, out string controllerId)
    {
        controllerId = null;

        return objectManager.TryGetId(issue.IssueOwner, out var issueOwnerId)
            && ownership.TryGetOwner(issueOwnerId, issue.StringId, out controllerId);
    }

    private void DismissQuest(IssueBase issue, bool announce)
    {
        Logger.Information("Dismissing the quest of {issue} offered by {owner}, its player is gone", issue.StringId, issue.IssueOwner?.StringId);

        issue.IssueFinalized();

        if (announce)
        {
            messageBroker.Publish(issue, new IssueOutcomeObserved(issue, IssueOutcome.QuestCancel));
        }
    }

    private void DismissTroops(IssueBase issue, bool announce)
    {
        Logger.Information("Dismissing the troops sent for {issue} offered by {owner}, nobody owns them", issue.StringId, issue.IssueOwner?.StringId);

        var settlement = issue.IssueOwner?.CurrentSettlement ?? issue.IssueOwner?.HomeSettlement;

        foreach (var element in issue.AlternativeSolutionSentTroops.GetTroopRoster())
        {
            if (element.Character.IsHero && element.Character.HeroObject.HeroState == Hero.CharacterStates.Disabled)
            {
                FreeCompanion(element.Character.HeroObject, settlement);
            }
        }

        issue.IssueFinalized();

        if (announce)
        {
            messageBroker.Publish(issue, new IssueOutcomeObserved(issue, IssueOutcome.AlternativeSolution));
        }
    }

    private static void FreeCompanion(Hero companion, Settlement settlement)
    {
        if (settlement == null)
        {
            companion.ChangeState(Hero.CharacterStates.Active);
            return;
        }

        TeleportHeroAction.ApplyImmediateTeleportToSettlement(companion, settlement);
    }
}
