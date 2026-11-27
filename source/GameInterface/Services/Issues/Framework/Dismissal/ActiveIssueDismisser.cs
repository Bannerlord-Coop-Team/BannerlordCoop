using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Registry.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Messages;
using GameInterface.Services.UI.Notifications;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>
/// When the server starts and whenever a player leaves,every registered issue being solved whose
/// player is gone is ended silently, so its notable does not stay stuck on it. A player whose
/// registration is deleted loses its issues right away, troops included, because its controller
/// can come back with a new character. For this first iteration of quests, this will be the
/// behavior, but this will change in the future. <see cref="End"/> is also the one recovery path
/// for the coordinator's and dispatcher's endings, so one that throws still ends and is announced.
/// </summary>
internal class ActiveIssueDismisser : IHandler, IActiveIssueDismisser
{
    private static readonly ILogger Logger = LogManager.GetLogger<ActiveIssueDismisser>();
    private const string ReturnedTroopsText = "{=V5Za6d4h}Your troops have returned from their mission.";

    private readonly IMessageBroker messageBroker;
    private readonly IQuestTypeRegistry registry;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IIssueDismisser issueDismisser;
    private readonly IIssueOwnerResolver ownerResolver;
    // Endings that threw while the issue was still in the manager, with the outcome it ends with. Its owner entry is gone,
    // so no sweep or tick would end it or tell the clients.
    private readonly Dictionary<IssueBase, IssueOutcome> failedDismissals = new(ReferenceComparer<IssueBase>.Instance);
    // Issues whose one ending already started, so no cancel, completion or troop return runs for them again
    private readonly ConditionalWeakTable<IssueBase, Claim> claims = new();

    private sealed class Claim
    {
        // The first ending published for the owner, even when no client got it, which its journal keeps
        public IssueBase.IssueUpdateDetails? SentEnding;
        // Its troop return ran, so they are never returned a second time
        public bool ReturnStarted;
    }

    public ActiveIssueDismisser(
        IMessageBroker messageBroker,
        IQuestTypeRegistry registry,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IIssueDismisser issueDismisser,
        IIssueOwnerResolver ownerResolver)
    {
        this.messageBroker = messageBroker;
        this.registry = registry;
        this.ownership = ownership;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.issueDismisser = issueDismisser;
        this.ownerResolver = ownerResolver;

        messageBroker.Subscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<PlayerRemoved>(Handle_PlayerRemoved);
        messageBroker.Subscribe<AlternativeSolutionEndingObserved>(RememberSentEnding);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AllGameObjectsRegistered>(Handle_AllGameObjectsRegistered);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<PlayerRemoved>(Handle_PlayerRemoved);
        messageBroker.Unsubscribe<AlternativeSolutionEndingObserved>(RememberSentEnding);
    }

    private void RememberSentEnding(MessagePayload<AlternativeSolutionEndingObserved> payload)
    {
        if (claims.TryGetValue(payload.What.Issue, out var claim)) claim.SentEnding ??= payload.What.Ending;
    }

    public bool TryClaim(IssueBase issue)
    {
        if (claims.TryGetValue(issue, out _)) return false;

        claims.Add(issue, new Claim());
        return true;
    }

    public void NoteTroopReturn(IssueBase issue)
    {
        if (claims.TryGetValue(issue, out var claim)) claim.ReturnStarted = true;
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

    // Owner entries point at a controller, so the removed player's issues would otherwise come back to its next character
    private void Handle_PlayerRemoved(MessagePayload<PlayerRemoved> payload)
    {
        var controllerId = payload.What.ControllerId;
        GameThread.RunSafe(() => Dismiss(announce: true, removedControllerId: controllerId), context: nameof(ActiveIssueDismisser));
    }

    private void Dismiss(bool announce, string removedControllerId = null)
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
                var playerIsGone = !TryGetControllerId(issue, out var owner) || owner == removedControllerId;
                if ((issue.IsSolvingWithQuest || issue.IsSolvingWithLordSolution) && !OwnerIsConnected(issue))
                {
                    End(issue, IssueOutcome.QuestCancel, announce: announce);
                }
                else if (issue.IsSolvingWithAlternative && playerIsGone)
                {
                    End(issue, IssueOutcome.AlternativeSolution, announce: announce);
                }
                else
                {
                    continue;
                }

                dismissed++;
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

    public void End(IssueBase issue, IssueOutcome outcome, Action ending = null, IssueBase.IssueUpdateDetails? closeWith = null,
        bool announce = true)
    {
        // Its ending already ran and the daily retry announces it, ending it again would rerun that ending with another outcome
        if (failedDismissals.ContainsKey(issue)) return;

        Finish(issue, outcome, ending ?? (issue.IsSolvingWithAlternative ? () => DismissTroops(issue) : () => DismissQuest(issue)),
            closeWith, announce);
    }

    public void EndThrown(IssueBase issue, IssueOutcome outcome, IDisposable owner)
    {
        // Its own ending got as far as it got, so it is only taken out of the manager, and the owner's open row ends as a cancel
        using (owner)
        {
            End(issue, outcome, () =>
            {
                if (issueDismisser.IsInManager(issue)) issueDismisser.Dismiss(issue);
            }, closeWith: IssueBase.IssueUpdateDetails.IssueCancel);
        }
    }

    private void Finish(IssueBase issue, IssueOutcome outcome, Action ending, IssueBase.IssueUpdateDetails? closeWith, bool announce)
    {
        objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId);
        // No ending takes a claim, so it is read once here, only the troop return below may add one
        claims.TryGetValue(issue, out var claim);

        // Runs as the owner. A throw before vanilla's troop return, or before its cancel claimed the issue, left the
        // troops out, so they come back the way vanilla's cancel brings them, with its returned line once they are back
        // when the owner was sent no ending yet.
        var returned = false;
        Step(issue, "Returning the troops of {issue} failed", () =>
        {
            if (closeWith == null || claim?.ReturnStarted == true || !issue.IsSolvingWithAlternative) return;
            // So no later cancel or completion returns them again
            claim ??= claims.GetValue(issue, _ => new Claim());
            Campaign.Current.IssueManager.TryToMakeTroopsReturn(issue);
            returned = true;
        });
        if (returned && claim.SentEnding == null) AddReturnedLog(issue);
        // A throw before vanilla's ending event left the owner's accepted journal open, so it is cancelled the way
        // vanilla's cancel does, which also sends it to the owner's client.
        Step(issue, "Ending the journal of {issue} failed", () =>
        {
            if (closeWith != null && claim?.SentEnding == null && issue.IsSolvingWithAlternative)
                CampaignEventDispatcher.Instance.OnIssueUpdated(issue, closeWith.Value, Hero.MainHero);
        });
        Step(issue, "Ending {issue} failed", ending);
        // A controller whose next character could collect it must not keep owning it, the giver's next issue keeps its own
        Step(issue, "Dropping the owner of {issue} failed", () =>
        {
            if (issueOwnerId != null && ownership.TryGetOwner(issueOwnerId, issue.StringId, out _)) ownership.Remove(issueOwnerId);
        });
        Step(issue, "Closing the journal of {issue} failed", () =>
        {
            // With no owner hero left to end them, unfinished rows go while ended history stays
            if (closeWith == null)
            {
                Campaign.Current.LogEntryHistory._logs.RemoveAll(
                    entry => entry is JournalLogEntry journal && journal.IsRelatedTo(issue) && !journal.IsEnded());
                return;
            }

            // Also when a listener threw before the journal one, an ending already written stays. The row keeps its
            // own lines, which for a quest also hold the quest's, gains the issue's lines that listener kept from it,
            // and ends with what the owner was sent.
            var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog<JournalLogEntry>(entry => entry.IsRelatedTo(issue));
            if (archive != null && !archive.IsEnded())
                archive.Update(archive.GetEntries().Union(issue.JournalEntries).ToMBList(), claim?.SentEnding ?? closeWith.Value);
        });

        if (issueDismisser.IsInManager(issue))
        {
            Logger.Warning("{issue} is still in the manager, it is ended again after the next daily tick", issue.StringId);
            failedDismissals[issue] = outcome;
            return;
        }

        failedDismissals.Remove(issue);
        // Clients still holding it would ignore the giver's next issue, so they hear now. Startup has nobody to tell.
        if (announce) messageBroker.Publish(issue, new IssueOutcomeObserved(issue, outcome, issueOwnerId));
    }

    public void RetryFailedDismissals()
    {
        foreach (var failed in failedDismissals.ToList())
        {
            var issue = failed.Key;
            try
            {
                // Its ending already ran, so the retry only takes it out of the manager. Finalizing one already out would
                // clear its hero's next issue, and when finalizing throws the deactivation alone still takes it out.
                Finish(issue, failed.Value, () =>
                {
                    if (!issueDismisser.IsInManager(issue)) return;

                    try
                    {
                        issueDismisser.Dismiss(issue);
                    }
                    finally
                    {
                        if (issueDismisser.IsInManager(issue))
                        {
                            Logger.Warning("Finalizing {issue} failed again, it is only deactivated so what its quest spawned can stay on the map", issue.StringId);
                            // Hardening for a throw before IssueFinalized's first line, like another mod's prefix. With its
                            // quest still attached, DeactivateIssue cancels that quest again instead of taking it out.
                            issue.IssueQuest = null;
                            Campaign.Current.IssueManager.DeactivateIssue(issue);
                        }
                    }
                }, closeWith: null, announce: true);
            }
            catch (Exception e)
            {
                // The other retries still run and nothing unwinds the daily tick
                Logger.Error(e, "Retrying the ending of {issue} failed", issue.StringId);
            }
        }
    }

    // One failing step must not skip the ones after it
    private static void Step(IssueBase issue, string failure, Action step)
    {
        try
        {
            step();
        }
        catch (Exception e)
        {
            Logger.Error(e, failure, issue.StringId);
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

    private void DismissQuest(IssueBase issue)
    {
        Logger.Information("Dismissing the quest of {issue} offered by {owner}, its player is gone", issue.StringId, issue.IssueOwner?.StringId);

        var quest = issue.IssueQuest;
        var journals = Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>();
        // An ending vanilla wrote before it threw stays, the cancel would overwrite it
        var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog<JournalLogEntry>(entry => entry.IsRelatedTo(issue));
        if (journals != null && archive?.IsEnded() != true && ownerResolver.TryResolveOwnerHero(issue, out var owner))
        {
            // IssueFinalized detaches the issue without raising vanilla's journal completion callback.
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
            {
                if (quest != null && quest.IsOngoing)
                    journals.OnQuestCompleted(quest, QuestBase.QuestCompleteDetails.Cancel);
                // The issue's accepted status takes precedence over the quest detail in IsEnded.
                journals.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.IssueCancel, owner);
            }
        }

        issueDismisser.Dismiss(issue);
    }

    private void DismissTroops(IssueBase issue)
    {
        Logger.Information("Dismissing the troops sent for {issue} offered by {owner}, their player is gone", issue.StringId, issue.IssueOwner?.StringId);

        AddReturnedLog(issue);
        messageBroker.Publish(issue, new AlternativeSolutionEndingObserved(
            issue, IssueBase.IssueUpdateDetails.IssueCancel));
        issueDismisser.Dismiss(issue);
    }

    // A throwing log listener must not keep the owner from hearing the ending
    private static void AddReturnedLog(IssueBase issue)
    {
        Step(issue, "Adding the troop return log of {issue} failed", () =>
        {
            if (!issue.JournalEntries.Any(log => log.LogText?.Value == ReturnedTroopsText))
                issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject(ReturnedTroopsText)));
        });
    }
}
