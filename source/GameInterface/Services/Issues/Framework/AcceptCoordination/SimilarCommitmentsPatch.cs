using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// Vanilla turns a player down when any issue of the same type is being solved, which in co-op counts
/// every player's issues. Other players' issues read as untaken while the check runs, so only this
/// player's own issues count, including one with no known owner yet, which is this player's accept
/// still waiting on the server.
/// </summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CheckPreconditions))]
internal class SimilarCommitmentsPatch
{
    /// <summary>Hides same-type issues other players are solving, and shows this player's lord solutions as quest solutions,
    /// for the check, keeping their states in __state.</summary>
    [HarmonyPrefix]
    private static void Prefix(IssueBase __instance, out List<(IssueBase Issue, IssueBase.IssueState State)> __state)
    {
        __state = new List<(IssueBase Issue, IssueBase.IssueState State)>();

        var issueManager = Campaign.Current?.IssueManager;
        if (issueManager == null ||
            !ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver) ||
            !ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider))
        {
            return;
        }

        foreach (var issue in issueManager.Issues.Values)
        {
            if (issue != __instance &&
                issue.GetType() == __instance.GetType() &&
                (issue.IsSolvingWithQuest || issue.IsSolvingWithAlternative || issue.IsSolvingWithLordSolution) &&
                ownerResolver.TryGetOwnerControllerId(issue, out var ownerControllerId) &&
                (ownerControllerId != controllerIdProvider.ControllerId || issue.IsSolvingWithLordSolution))
            {
                __state.Add((issue, issue._issueState));
            }
        }

        using (new AllowedThread())
        {
            foreach (var hidden in __state)
            {
                var localLord = hidden.State == IssueBase.IssueState.SolvingWithLordSolution &&
                    ownerResolver.TryGetOwnerControllerId(hidden.Issue, out var owner) && owner == controllerIdProvider.ControllerId;
                hidden.Issue._issueState = localLord ? IssueBase.IssueState.SolvingWithQuestSolution : IssueBase.IssueState.Ongoing;
            }
        }
    }

    /// <summary>Puts back the issue states the prefix changed, even when the check throws.</summary>
    [HarmonyFinalizer]
    private static void Finalizer(List<(IssueBase Issue, IssueBase.IssueState State)> __state)
    {
        if (__state == null)
        {
            return;
        }

        using (new AllowedThread())
        {
            foreach (var hidden in __state)
            {
                hidden.Issue._issueState = hidden.State;
            }
        }
    }
}
