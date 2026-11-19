using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>
/// Another player's issue is marked as taken here, but it is not this player's quest, so the
/// quest screen does not list it. A mission is this player's only when the accept this client saw, or the
/// snapshot it got when joining, named this player.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, typeof(Action))]
internal class QuestScreenIssueFilterPatch
{
    [HarmonyPrefix]
    private static void Prefix(out List<(IssueBase Issue, IssueBase.IssueState State)> __state)
    {
        __state = new List<(IssueBase, IssueBase.IssueState)>();

        var issueManager = Campaign.Current?.IssueManager;
        if (issueManager == null || !ContainerProvider.TryResolve<IIssueMissionVisibility>(out var visibility))
        {
            return;
        }

        __state.AddRange(issueManager.Issues.Values.Where(visibility.IsAnotherPlayersTroopsMission).Select(issue => (issue, issue._issueState)));
        SetState(__state, restore: false);
    }

    [HarmonyFinalizer]
    private static void Finalizer(List<(IssueBase Issue, IssueBase.IssueState State)> __state)
    {
        if (__state == null)
        {
            return;
        }

        SetState(__state, restore: true);
    }

    internal static void SetState(List<(IssueBase Issue, IssueBase.IssueState State)> issues, bool restore)
    {
        using (new AllowedThread())
        {
            foreach (var issue in issues)
            {
                issue.Issue._issueState = restore ? issue.State : IssueBase.IssueState.Ongoing;
            }
        }
    }

    internal static void SetState(List<IssueBase> issues, IssueBase.IssueState state)
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
