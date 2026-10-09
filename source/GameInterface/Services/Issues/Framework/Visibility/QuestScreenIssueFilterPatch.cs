using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>
/// Another player's troops mission is marked as taken here, but it is not this player's quest, so the
/// quest screen does not list it. A mission is this player's only when the accept this client saw, or the
/// snapshot it got when joining, named this player.
/// </summary>
[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, typeof(Action))]
internal class QuestScreenIssueFilterPatch
{
    [HarmonyPrefix]
    private static void Prefix(out List<IssueBase> __state)
    {
        __state = new List<IssueBase>();

        var issueManager = Campaign.Current?.IssueManager;
        if (issueManager == null)
        {
            return;
        }

        __state.AddRange(issueManager.Issues.Values.Where(IsAnotherPlayersTroopsMission));
        SetState(__state, IssueBase.IssueState.Ongoing);
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

    internal static bool IsAnotherPlayersTroopsMission(IssueBase issue)
    {
        if (!issue.IsSolvingWithAlternative)
        {
            return false;
        }

        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider))
        {
            return false;
        }

        return !(objectManager.TryGetId(issue.IssueOwner, out var issueOwnerId)
            && ownership.TryGetOwner(issueOwnerId, issue.StringId, out var ownerControllerId)
            && ownerControllerId == controllerIdProvider.ControllerId);
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
