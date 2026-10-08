using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Patches;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

[HarmonyPatch]
internal sealed class ArtisanProductAlternativeSelectionPatches
{
    [HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.Initialize))]
    [HarmonyPrefix]
    private static void PrepareSelection(PartyScreenLogic __instance, ref PartyScreenLogicInitializationData initializationData)
    {
        if (ModInformation.IsServer) return;
        if (IssuesCampaignBehavior.GetIssueOwnersIssue() is not Issue issue || !issue.IsOngoingWithoutQuest ||
            initializationData.LeftMemberRoster != issue.AlternativeSolutionSentTroops ||
            initializationData.RightOwnerParty != PartyBase.MainParty) return;
        if (!ContainerProvider.TryResolve<IArtisanProductAlternativeSelection>(out var selection))
            throw new InvalidOperationException("Artisan selection service is unavailable");
        selection.Prepare(issue, ref initializationData);
        __instance._partyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage;
    }

    [HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.Initialize))]
    [HarmonyPostfix]
    private static void IsolateSelectionReset(PartyScreenLogic __instance)
    {
        if (__instance.PartyPresentationDoneButtonDelegate.Target is not IArtisanProductAlternativeSelection) return;
        __instance.CurrentData.RightItemRoster = new ItemRoster(__instance.CurrentData.RightItemRoster);
        __instance.CurrentData.RightParty = null;
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void RestoreSelectionAfterClaim(IssueBase __instance)
    {
        if (ModInformation.IsServer || __instance is not Issue issue) return;
        if (CallOriginalPolicy.IsOriginalAllowed() || IssueDispatchReplayGuard.IsActive) return;
        if (QuestTypeRegistry.Get(issue)?.SupportsAlternativeAccept != true || !issue.IsOngoingWithoutQuest) return;

        // The generic trigger has packed the claim; the selection never removed local troops.
        using (new AllowedThread())
        {
            issue.AlternativeSolutionSentTroops.Clear();
        }
    }

    [HarmonyPatch(typeof(IssuesCampaignBehavior), "issue_offer_player_accept_alternative_5_b_consequence")]
    [HarmonyPrefix]
    private static bool DiscardSelection()
    {
        if (ModInformation.IsServer || IssuesCampaignBehavior.GetIssueOwnersIssue() is not Issue issue) return true;
        if (issue.IsOngoingWithoutQuest)
            using (new AllowedThread()) issue.AlternativeSolutionSentTroops.Clear();
        return false;
    }

    [HarmonyPatch(typeof(IssuesCampaignBehavior), "issue_offer_player_accept_alternative_5_a_condition")]
    [HarmonyPrefix]
    private static bool RejectClosedIssue(ref bool __result)
    {
        if (ModInformation.IsServer || IssuesCampaignBehavior.GetIssueOwnersIssue() is not Issue issue ||
            issue.IsOngoingWithoutQuest) return true;
        __result = false;
        return false;
    }
}
