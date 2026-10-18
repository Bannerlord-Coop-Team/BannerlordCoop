using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd.Patches;

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal static class HeadmanHerdDailyTickPatch
{
    [HarmonyPrefix]
    private static void Prefix(out List<KeyValuePair<Hero, IssueBase>> __state)
    {
        __state = new List<KeyValuePair<Hero, IssueBase>>();
        if (ModInformation.IsServer) return;
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners)) return;
        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerId)) return;

        var issueManager = Campaign.Current?.IssueManager;
        if (issueManager == null) return;

        foreach (var pair in issueManager._issues.ToList())
        {
            if (!(pair.Value is Issue issue) || !issue.IsSolvingWithAlternative || issue.JournalEntries.Count != 0 ||
                !owners.TryGetOwnerControllerId(issue, out var owner) || owner == controllerId.ControllerId) continue;

            issueManager._issues.Remove(pair.Key);
            __state.Add(pair);
        }
    }

    [HarmonyPostfix]
    private static void Postfix(List<KeyValuePair<Hero, IssueBase>> __state)
    {
        if (__state == null || __state.Count == 0 || Campaign.Current?.IssueManager == null) return;
        foreach (var pair in __state) Campaign.Current.IssueManager._issues[pair.Key] = pair.Value;
    }
}
