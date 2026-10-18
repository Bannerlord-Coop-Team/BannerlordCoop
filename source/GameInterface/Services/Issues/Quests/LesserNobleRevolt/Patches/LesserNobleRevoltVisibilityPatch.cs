using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, typeof(Action))]
internal static class LesserNobleRevoltVisibilityPatch
{
    [HarmonyPrefix]
    private static void Prefix(out List<IssueBase> __state)
    {
        __state = new List<IssueBase>();
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var resolver) ||
            !ContainerProvider.TryResolve<IControllerIdProvider>(out var controller))
        {
            return;
        }

        foreach (var issue in Campaign.Current.IssueManager.Issues.Values.Where(value => value is Issue))
        {
            if (resolver.TryGetOwnerControllerId(issue, out var owner) && owner != controller.ControllerId)
            {
                __state.Add(issue);
                issue._issueState = IssueBase.IssueState.Ongoing;
            }
        }
    }

    [HarmonyFinalizer]
    private static void Finalizer(List<IssueBase> __state)
    {
        if (__state == null) return;
        using (new AllowedThread())
        foreach (var issue in __state) issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
    }
}
