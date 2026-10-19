using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Issue availability and expiry are authoritative on the campaign server.</summary>
[HarmonyPatch]
internal static class IssueManagerClientLifecyclePatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.DailyTick));
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnSettlementEntered));
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnSettlementLeft));
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnCharacterPortraitPopUpOpened));
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    private static class HeroKilledPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Hero victim, out IDisposable __state)
        {
            __state = null;
            if (ModInformation.IsClient)
            {
                return false;
            }

            if (victim?.Issue == null ||
                !ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver) ||
                !ownerResolver.TryResolveOwnerHero(victim.Issue, out var ownerHero))
            {
                return false;
            }

            __state = new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo);
            return true;
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, IDisposable __state)
        {
            __state?.Dispose();
            return __exception;
        }
    }

    [HarmonyPrefix]
    private static bool Prefix(IssueManager __instance, MethodBase __originalMethod)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        if (__originalMethod.Name == nameof(IssueManager.DailyTick))
        {
            AdvanceAlternativeSolutionJournalProgress(__instance);
        }

        return false;
    }

    private static void AdvanceAlternativeSolutionJournalProgress(IssueManager issueManager)
    {
        foreach (var issue in issueManager.Issues.Values)
        {
            if (!issue.IsSolvingWithAlternative ||
                issue.AlternativeSolutionReturnTimeForTroops.IsPast ||
                GameInterface.Services.Issues.Framework.Visibility.QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(issue))
            {
                continue;
            }

            if ((int)issue.AlternativeSolutionIssueEffectClearTime.ToDays == (int)CampaignTime.Now.ToDays)
            {
                issue.OnAlternativeSolutionSolvedAndTroopsAreReturning();
            }

            if (issue.JournalEntries.Count == 0)
            {
                continue;
            }

            var journalLog = issue.JournalEntries[0];
            journalLog.UpdateCurrentProgress(Math.Min(journalLog.CurrentProgress + 1, journalLog.Range));
        }
    }
}
