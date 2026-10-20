using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd.Patches;

[HarmonyPatch]
internal class HeadmanHerdWorldEventPatches
{
    private sealed class GiverKilledState : IDisposable
    {
        internal readonly IDisposable OwnerScope;
        internal readonly Issue Issue;

        internal GiverKilledState(IDisposable ownerScope, Issue issue)
        {
            OwnerScope = ownerScope;
            Issue = issue;
        }

        public void Dispose() => OwnerScope?.Dispose();
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool GiverKilledPrefix(Hero victim, out GiverKilledState __state)
    {
        __state = null;
        if (!(victim.Issue is Issue issue)) return true;
        if (ModInformation.IsClient) return false;
        if (issue.IssueQuest == null && !issue.IsSolvingWithAlternative) return true;
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) || !owners.TryResolveOwnerHero(issue, out var owner)) return false;
        if (owner.PartyBelongedTo == null && !owner.IsPrisoner)
        {
            if (ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var pending)) pending.RememberGiverDeath(issue);
            return false;
        }
        if (issue.IsSolvingWithAlternative && owner.PartyBelongedTo != null &&
            ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup))
            cleanup.Remember(issue, owner.PartyBelongedTo);
        __state = new GiverKilledState(new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo), issue);
        // Keep the owner on the issue until vanilla can return the troops without its global queue.
        if (issue.IsSolvingWithAlternative && !Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) return false;
        return true;
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    [HarmonyFinalizer]
    private static void GiverKilledFinalizer(GiverKilledState __state) => __state?.Dispose();

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPrefix]
    private static void AlternativeSolutionStartedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsClient || !(__instance is Issue) ||
            !ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) ||
            !owners.TryResolveOwnerHero(__instance, out var owner) || owner.PartyBelongedTo == null ||
            !ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup))
            return;

        cleanup.Remember(__instance, owner.PartyBelongedTo);
    }

    [HarmonyPatch(typeof(KillCharacterAction), nameof(KillCharacterAction.ApplyInternal))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void GiverDeathStartedPrefix(Hero victim, out Issue __state)
    {
        __state = null;
        if (!ModInformation.IsServer || !victim.IsAlive || !(victim.Issue is Issue issue))
            return;

        if (ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup)) cleanup.RememberGiverDeath(issue);
        __state = issue;
    }

    [HarmonyPatch(typeof(KillCharacterAction), nameof(KillCharacterAction.ApplyInternal))]
    [HarmonyPostfix]
    private static void GiverDeathCompletedPostfix(Hero victim, Issue __state)
    {
        if (__state == null) return;
        if (victim.IsAlive)
        {
            return;
        }

        if (__state.IsSolvingWithAlternative)
        {
            TryCancelGiver(__state);
        }
        else if (__state.IssueQuest == null) TryCancelGiver(__state);
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.TryToMakeTroopsReturn))]
    [HarmonyPrefix]
    private static bool ReturnAlternativeTroopsPrefix(IssueBase issue)
    {
        if (ModInformation.IsClient || !(issue is Issue) ||
            !ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup))
            return true;

        return !cleanup.ReturnAlternativeTroops(issue);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void RetryCanceledCompanionsPostfix()
    {
        if (ModInformation.IsClient) return;
        if (!ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup)) return;
        var issues = cleanup.PendingGiverDeaths.Concat(cleanup.RememberedIssues).OfType<Issue>().Distinct().ToList();
        foreach (var issue in issues)
            TryCancelGiver(issue);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool CancelInsteadOfCompletingPrefix(IssueBase __instance)
    {
        if (!(__instance is Issue issue) || issue.IssueOwner.IsAlive) return true;
        if (ModInformation.IsServer) TryCancelGiver(issue);
        return false;
    }

    private static void TryCancelGiver(Issue issue)
    {
        if (!ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup)) return;
        if (issue.IssueOwner.IsAlive)
        {
            cleanup.ForgetGiverDeath(issue);
            return;
        }

        if (issue.IsSolvingWithAlternative && !cleanup.HasRememberedParty(issue))
        {
            cleanup.ForgetGiverDeath(issue);
            return;
        }
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) ||
            !owners.TryResolveOwnerHero(issue, out var owner)) return;
        if (owner.PartyBelongedTo == null && !owner.IsPrisoner) return;

        using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
        {
            if (issue.IsSolvingWithAlternative && !Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) return;

            if (issue.IssueOwner.Issue == issue)
                Campaign.Current.IssueManager.OnHeroKilled(issue.IssueOwner, null, KillCharacterAction.KillCharacterActionDetail.None, false);
            else
                issue.CompleteIssueWithCancel();
        }

        cleanup.ForgetGiverDeath(issue);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnHeroKilled))]
    [HarmonyPrefix]
    private static bool HeroKilledPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapEventStartedPrefix() => ModInformation.IsServer;

}
