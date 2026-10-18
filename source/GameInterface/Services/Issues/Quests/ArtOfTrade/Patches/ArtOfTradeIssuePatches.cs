using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue;

namespace GameInterface.Services.Issues.Quests.ArtOfTrade.Patches;

[HarmonyPatch]
internal class ArtOfTradeIssuePatches
{
    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.TryToMakeTroopsReturn))]
    [HarmonyPrefix]
    private static bool ReturningTroops(IssueBase issue)
    {
        if (!(issue is Issue) || ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed() ||
            Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) return true;
        MessageBroker.Instance.Publish(issue, new ArtOfTradeTroopsDeferred(issue));
        return false;
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.HourlyTick))]
    [HarmonyPostfix]
    private static void HourlyTick()
    {
        if (ModInformation.IsServer) MessageBroker.Instance.Publish(typeof(ArtOfTradeIssuePatches), new ArtOfTradeHourlyTick());
    }

    [HarmonyPatch(typeof(JournalLog), nameof(JournalLog.UpdateCurrentProgress))]
    [HarmonyPrefix]
    private static bool JournalProgress(JournalLog __instance)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            if (issue is Issue && issue.IsSolvingWithAlternative && issue.JournalEntries.Contains(__instance)) return false;
        }
        return true;
    }

    // The owner gets the returning log and resolved effects with the server's daily journal, adding them here too repeats the log
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.OnAlternativeSolutionSolvedAndTroopsAreReturning))]
    [HarmonyPrefix]
    private static bool TroopsReturning(IssueBase __instance)
    {
        if (!(__instance is Issue) || ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;
        return !(ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) &&
            ContainerProvider.TryResolve<IControllerIdProvider>(out var controller) &&
            owners.TryGetOwnerControllerId(__instance, out var owner) && owner == controller.ControllerId);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
    [HarmonyPrefix]
    private static bool Cancel(IssueBase __instance, out (IssueBase Issue, IDisposable Owner, IssueOutcome Outcome) __state)
    {
        __state = default;
        if (!(__instance is Issue) || CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;

        if (ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) && owners.TryResolveOwnerHero(__instance, out var hero))
            __state.Owner = new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo);

        // A direct quest announces its own ending; an offer or troops mission has no quest to do it.
        if (__instance.IssueQuest == null) __state.Issue = __instance;
        __state.Outcome = __instance.IsSolvingWithAlternative ? IssueOutcome.AlternativeSolution : IssueOutcome.QuestCancel;
        return true;
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
    [HarmonyPostfix]
    private static void Canceled((IssueBase Issue, IDisposable Owner, IssueOutcome Outcome) __state)
    {
        if (__state.Issue != null)
            MessageBroker.Instance.Publish(__state.Issue, new IssueOutcomeObserved(__state.Issue, __state.Outcome));
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
    [HarmonyFinalizer]
    private static void RestoreOwner((IssueBase Issue, IDisposable Owner, IssueOutcome Outcome) __state) => __state.Owner?.Dispose();

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAiLord))]
    [HarmonyPrefix]
    private static bool AiLord(IssueBase __instance, out IssueBase __state)
    {
        __state = null;
        if (!(__instance is Issue) || CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;
        __state = __instance;
        return true;
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAiLord))]
    [HarmonyPostfix]
    private static void AiLordFinished(IssueBase __state)
    {
        if (__state != null) MessageBroker.Instance.Publish(__state, new IssueOutcomeObserved(__state, IssueOutcome.QuestCancel));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueUpdated))]
    [HarmonyPrefix]
    private static void AlternativeEnding(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (ModInformation.IsServer && issue is Issue && issue.IsSolvingWithAlternative &&
            (details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest ||
             details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest || details == IssueBase.IssueUpdateDetails.IssueCancel))
            MessageBroker.Instance.Publish(issue, new ArtOfTradeIssueJournalChanged(issue, details));
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
    [HarmonyPostfix]
    private static void AlternativeProgress()
    {
        if (ModInformation.IsClient) return;
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            if (issue is Issue && issue.IsSolvingWithAlternative)
                MessageBroker.Instance.Publish(issue, new ArtOfTradeIssueJournalChanged(issue, IssueBase.IssueUpdateDetails.None));
        }
    }
}
