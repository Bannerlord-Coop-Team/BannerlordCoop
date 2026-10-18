using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
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
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnHeroKilled));
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
            if (!issue.IsSolvingWithAlternative || issue.AlternativeSolutionReturnTimeForTroops.IsPast)
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
