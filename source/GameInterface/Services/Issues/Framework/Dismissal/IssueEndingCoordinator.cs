using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>
/// Runs every ending the sweep and the dispatch services hand over step by step, so one that throws still ends once,
/// and keeps an issue still in the manager for the daily retry.
/// </summary>
internal class IssueEndingCoordinator : IIssueEndingCoordinator
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueEndingCoordinator>();

    private readonly IMessageBroker messageBroker;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IObjectManager objectManager;
    private readonly IIssueDismisser issueDismisser;
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IIssueEndingClaimRegistry claims;
    // Issues whose ending threw while they were still in the manager, with their outcome, until the daily retry removes them
    private readonly Dictionary<IssueBase, IssueOutcome> failedEndings = new(ReferenceComparer<IssueBase>.Instance);

    public IssueEndingCoordinator(
        IMessageBroker messageBroker,
        IIssueOwnershipRegistry ownership,
        IObjectManager objectManager,
        IIssueDismisser issueDismisser,
        IIssueOwnerResolver ownerResolver,
        IIssueEndingClaimRegistry claims)
    {
        this.messageBroker = messageBroker;
        this.ownership = ownership;
        this.objectManager = objectManager;
        this.issueDismisser = issueDismisser;
        this.ownerResolver = ownerResolver;
        this.claims = claims;
    }

    public void EndWithoutOwner(IssueBase issue, IssueOutcome outcome, bool announce)
    {
        // The daily retry finishes an issue whose ending threw, ending the issue again would rerun that ending with another outcome
        if (failedEndings.ContainsKey(issue)) return;

        if (issue.IsSolvingWithAlternative) FinishWithoutOwner(issue, outcome, () => DismissTroops(issue), announce);
        else FinishWithoutOwner(issue, outcome, () => DismissQuest(issue), announce);
    }

    public void EndAsOwner(IssueBase issue, IssueOutcome outcome, Action ending, IDisposable owner)
    {
        using (owner)
        {
            if (failedEndings.ContainsKey(issue)) return;

            IssueEndingClaim claim = null;
            if (issue.IsSolvingWithAlternative)
            {
                claim = ReturnTroopsIfStillOut(issue);
                CancelOpenJournal(issue, claim);
            }
            // An ending already written stays, an open row ends with what the owner was sent, read once the ending ran
            EndAndAnnounce(issue, outcome, ending,
                () => issueDismisser.CloseJournal(issue, claim?.SentEnding ?? IssueBase.IssueUpdateDetails.IssueCancel), announce: true);
        }
    }

    public Exception EndAfterThrow(IssueBase issue, IssueOutcome outcome, IDisposable owner, Exception exception)
    {
        Logger.Error(exception, "Ending {issue} with {outcome} threw, it is ended without running vanilla's ending again", issue.StringId, outcome);
        // Vanilla's ending got as far as it got, so the issue is only taken out of the manager
        EndAsOwner(issue, outcome, () => issueDismisser.Remove(issue), owner);
        return null;
    }

    public void RetryFailedEndings()
    {
        // The ending already ran, so the retry only takes the issue out of the manager
        foreach (var failed in failedEndings.ToList())
            FinishWithoutOwner(failed.Key, failed.Value, () => issueDismisser.ForceRemove(failed.Key), announce: true);
    }

    // With no owner hero left to end them, the issue's unfinished journal rows go
    private void FinishWithoutOwner(IssueBase issue, IssueOutcome outcome, Action ending, bool announce) =>
        EndAndAnnounce(issue, outcome, ending, () => issueDismisser.DropUnfinishedJournal(issue), announce);

    // Clients still holding the issue would ignore the giver's next issue, so they hear once it is out of the manager
    private void EndAndAnnounce(IssueBase issue, IssueOutcome outcome, Action ending, Action closeJournal, bool announce)
    {
        var hasOwnerId = objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId);
        Step(issue, "Ending {issue} failed", ending);
        if (hasOwnerId) ownership.Remove(issueOwnerId, issue.StringId);
        Step(issue, "Closing the journal of {issue} failed", closeJournal);

        if (issueDismisser.IsInManager(issue))
        {
            Logger.Warning("{issue} is still in the manager, it is ended again after the next daily tick", issue.StringId);
            failedEndings[issue] = outcome;
            return;
        }

        failedEndings.Remove(issue);
        if (announce) messageBroker.Publish(issue, new IssueOutcomeObserved(issue, outcome, issueOwnerId));
    }

    // A throw before vanilla's troop return, or before the cancel claimed the issue, left the sent troops out
    private IssueEndingClaim ReturnTroopsIfStillOut(IssueBase issue)
    {
        // Claimed before the return, so no later cancel or completion returns the troops again
        var claim = claims.GetOrClaim(issue);
        if (claim.ReturnStarted) return claim;

        var returned = Step(issue, "Returning the troops of {issue} failed", () => issueDismisser.ReturnTroops(issue));
        // An ending the owner was already sent carries vanilla's own report of the troops
        if (returned && claim.SentEnding == null) AddTroopsReturnedLog(issue);
        return claim;
    }

    // A throw before vanilla's ending event left the owner's accepted journal row open, so it is cancelled like vanilla's cancel
    private void CancelOpenJournal(IssueBase issue, IssueEndingClaim claim)
    {
        if (claim.SentEnding != null) return;

        Step(issue, "Cancelling the journal of {issue} failed", () => issueDismisser.SendCancel(issue));
    }

    // One failing step must not skip the ones after it. True when the step ran through.
    private static bool Step(IssueBase issue, string failure, Action step)
    {
        try
        {
            step();
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, failure, issue.StringId);
            return false;
        }
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

        AddTroopsReturnedLog(issue);
        messageBroker.Publish(issue, new AlternativeSolutionEndingObserved(
            issue, IssueBase.IssueUpdateDetails.IssueCancel));
        issueDismisser.Dismiss(issue);
    }

    // A throwing log listener must not keep the owner from hearing the ending
    private void AddTroopsReturnedLog(IssueBase issue) =>
        Step(issue, "Adding the troop return log of {issue} failed", () => issueDismisser.AddTroopsReturnedLog(issue));
}
