using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Issues;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Issue generation runs every settlement/clan tick: <see cref="IssueManager.CheckForIssues"/>
    /// evaluates each issue type's condition and <see cref="IssueManager.CreateNewIssue"/> constructs
    /// the chosen one. Several issue types fault on an arbitrary headless-loaded world (missing
    /// market/village/notable data). Absorb per-issue failures at these two chokepoints so issue
    /// generation never aborts the tick — the affected issues simply don't spawn.
    /// </summary>
    [HarmonyPatch]
    internal class IssuePatches
    {
        [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.CheckForIssues))]
        [HarmonyFinalizer]
        static Exception CheckForIssuesFinalizer(ref List<PotentialIssueData> __result, Exception __exception)
        {
            if (__exception != null) __result = new List<PotentialIssueData>();
            return null;
        }

        [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.CreateNewIssue))]
        [HarmonyFinalizer]
        static Exception CreateNewIssueFinalizer(ref bool __result, Exception __exception)
        {
            if (__exception != null) __result = false;
            return null;
        }
    }
}
