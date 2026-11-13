using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>
/// A client only marks another player's troops mission as taken, it never starts it, so the issue usually has no
/// journal there. Vanilla's daily tick advances the first journal entry of every troops mission and would throw on it
/// every day, so the client leaves other players' missions out of the tick. The server completes them.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class ForeignTroopsMissionDailyTickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueManager __instance, out List<IssueBase> __state)
    {
        __state = null;

        if (ModInformation.IsServer)
        {
            return true;
        }

        // A client that joined later loaded the server's journal for it, ticking that would mark a hidden quest as updated
        __state = __instance.Issues.Values
            .Where(issue => issue.IsSolvingWithAlternative &&
                (issue.JournalEntries.Count == 0 || QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(issue)))
            .ToList();

        // A taken state the daily tick has nothing to do for
        QuestScreenIssueFilterPatch.SetState(__state, IssueBase.IssueState.SolvingWithQuestSolution);

        foreach (var issue in __instance.Issues.Values)
        {
            if (!issue.IsSolvingWithAlternative ||
                issue.AlternativeSolutionReturnTimeForTroops.IsPast ||
                QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(issue))
            {
                continue;
            }

            if ((int)issue.AlternativeSolutionIssueEffectClearTime.ToDays == (int)CampaignTime.Now.ToDays)
            {
                issue.OnAlternativeSolutionSolvedAndTroopsAreReturning();
            }

            if (issue.JournalEntries.Count > 0)
            {
                var journalLog = issue.JournalEntries[0];
                journalLog.UpdateCurrentProgress(Math.Min(journalLog.CurrentProgress + 1, journalLog.Range));
            }
        }

        return false;
    }

    [HarmonyFinalizer]
    private static void Finalizer(List<IssueBase> __state)
    {
        if (__state == null)
        {
            return;
        }

        // Vanilla logs the return here, but only the owner may see that entry.
        foreach (var issue in __state
                     .Where(issue => (int)issue.AlternativeSolutionIssueEffectClearTime.ToDays <= (int)CampaignTime.Now.ToDays))
        {
            issue._areIssueEffectsResolved = true;
        }

        QuestScreenIssueFilterPatch.SetState(__state, IssueBase.IssueState.SolvingWithAlternativeSolution);
    }
}
