using Common;
using Common.Util;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>
/// A client only marks another player's troops mission as taken, it never starts it, so the issue has no journal
/// there. Vanilla's daily tick advances the first journal entry of every troops mission and would throw on it every
/// day, so the client leaves those missions out of the tick. The server completes them.
/// </summary>
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class ForeignTroopsMissionDailyTickPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueManager __instance, out List<IssueBase> __state)
    {
        __state = null;

        if (ModInformation.IsServer)
        {
            return;
        }

        __state = __instance.Issues.Values
            .Where(issue => issue.IsSolvingWithAlternative && issue.JournalEntries.Count == 0)
            .ToList();

        // A taken state the daily tick has nothing to do for
        SetState(__state, IssueBase.IssueState.SolvingWithQuestSolution);
    }

    [HarmonyFinalizer]
    private static void Finalizer(List<IssueBase> __state)
    {
        if (__state == null)
        {
            return;
        }

        SetState(__state, IssueBase.IssueState.SolvingWithAlternativeSolution);
    }

    private static void SetState(List<IssueBase> issues, IssueBase.IssueState state)
    {
        using (new AllowedThread())
        {
            foreach (var issue in issues)
            {
                issue._issueState = state;
            }
        }
    }
}
