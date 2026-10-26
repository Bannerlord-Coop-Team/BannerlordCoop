using Common;
using Common.Logging;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Keeps cancellation out of vanilla's ownerless deferred troop return queue.</summary>
internal class IssueCancellationCoordinator : IIssueCancellationCoordinator
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueCancellationCoordinator>();
    private readonly IQuestTypeRegistry registry;
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IActiveIssueDismisser dismisser;
    private readonly Dictionary<IssueBase, TextObject> pending = new Dictionary<IssueBase, TextObject>();

    public IssueCancellationCoordinator(IQuestTypeRegistry registry, IIssueOwnerResolver ownerResolver, IActiveIssueDismisser dismisser)
    {
        this.registry = registry;
        this.ownerResolver = ownerResolver;
        this.dismisser = dismisser;
    }

    public bool IsPending(IssueBase issue) => issue != null && pending.ContainsKey(issue);

    public bool BeforeGiverDeath(Hero victim, out IDisposable owner)
    {
        owner = null;
        if (victim.Issue == null) return true;
        if (!PrepareCancellation(victim.Issue, out owner))
        {
            if (victim.Issue.IssueQuest?.IsOngoing == true && registry.IsRegistered(victim.Issue.GetType()))
                pending[victim.Issue] = BuildGiverDeathLog(victim.Issue);
            return false;
        }

        if (victim.Issue.IssueQuest != null && victim.Issue.IssueQuest.IsOngoing && registry.IsRegistered(victim.Issue.GetType()))
        {
            CancelQuestAndIssue(victim.Issue, BuildGiverDeathLog(victim.Issue));
            return false;
        }

        return true;
    }

    public void CancelForPlayerDeath(string controllerId, Hero oldHero, MobileParty originalParty)
    {
        if (ModInformation.IsClient || !oldHero.IsDead || originalParty == null) return;

        using (new MainHeroSubstitutionScope(oldHero, originalParty))
        {
            foreach (var issue in Campaign.Current.IssueManager.Issues.Values.ToList())
            {
                var quest = issue.IssueQuest;
                if (!registry.IsRegistered(issue.GetType()) || quest == null || !quest.IsOngoing || quest.IsSpecialQuest ||
                    !ownerResolver.TryGetOwnerControllerId(issue, out var owner) || owner != controllerId) continue;

                try { CancelQuestAndIssue(issue, new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.")); }
                catch (Exception e) { Logger.Error(e, "Cancelling quest {questId} during heir selection failed", quest.StringId); }
            }
        }
    }

    public bool BeforeCancellation(IssueBase issue, TextObject log, out IDisposable owner)
    {
        if (!PrepareCancellation(issue, out owner))
        {
            if (log != null) pending[issue] = log;
            return false;
        }

        if (issue.IssueQuest != null && issue.IssueQuest.IsOngoing && registry.IsRegistered(issue.GetType()))
        {
            // OnQuestCompleted ends the issue; letting the enclosing issue method run would end it twice.
            CancelQuestAndIssue(issue, log);
            return false;
        }

        return true;
    }

    private bool PrepareCancellation(IssueBase issue, out IDisposable owner)
    {
        owner = null;
        if (!registry.IsRegistered(issue.GetType())) return true;

        if (ModInformation.IsClient)
        {
            if (!QuestEndingReplay.IsActive) return false;
            // Troops are already returned by the server; replay only detaches this issue.
            if (issue.IssueQuest == null && issue.IsSolvingWithAlternative)
            {
                issue.IssueFinalized();
                return false;
            }
            return true;
        }

        if (issue.IssueOwner.Issue != issue)
        {
            pending.Remove(issue);
            return false;
        }

        // OnQuestCompleted only detaches the already finalized quest; its consequences have run.
        if (issue.IssueQuest != null && !issue.IssueQuest.IsOngoing) return true;

        if (issue.IssueQuest == null && !issue.IsSolvingWithAlternative) return true;

        if (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero))
        {
            pending.Remove(issue);
            dismisser.Dismiss(issue, IssueOutcome.QuestCancel);
            return false;
        }

        if (ownerHero.PartyBelongedTo == null)
        {
            pending.TryAdd(issue, null);
            return false;
        }

        var scope = new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo);
        try
        {
            if (issue.IsSolvingWithAlternative && !Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution())
            {
                pending.TryAdd(issue, null);
                return false;
            }

            pending.Remove(issue);
            owner = scope;
            return true;
        }
        finally
        {
            if (owner == null) scope.Dispose();
        }
    }

    public void Retry()
    {
        if (ModInformation.IsClient) return;

        foreach (var entry in pending.ToList())
        {
            var issue = entry.Key;
            try
            {
                if (issue.IssueOwner.Issue != issue)
                {
                    pending.Remove(issue);
                    continue;
                }

                issue.CompleteIssueWithCancel(entry.Value);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Retrying cancellation of {issue} failed", issue.StringId);
            }
        }
    }

    private static TextObject BuildGiverDeathLog(IssueBase issue)
    {
        var text = new TextObject("{=rTvWdMXF}{DIED_HERO.LINK} died and your agreement with {?DIED_HERO.GENDER}her{?}him{\\?} canceled.");
        StringHelpers.SetCharacterProperties("DIED_HERO", issue.IssueOwner.CharacterObject, text);
        return text;
    }

    private void CancelQuestAndIssue(IssueBase issue, TextObject log)
    {
        var quest = issue.IssueQuest;
        if (quest != null && quest.IsOngoing)
            quest.CompleteQuestWithCancel(log);

        if (issue.IssueOwner?.Issue == issue)
        {
            issue.IssueFinalized();
            if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
                dispatcher.After(issue, IssueOutcome.QuestCancel);
        }
    }
}
