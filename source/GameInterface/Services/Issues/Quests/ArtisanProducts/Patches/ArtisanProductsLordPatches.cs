using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts.Patches;

[HarmonyPatch]
internal class ArtisanProductsLordPatches
{
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithLordSolution))]
    [HarmonyPrefix]
    private static bool Start(IssueBase __instance) => Before(__instance, ArtisanProductsLordAction.Start);

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithLordSolutionWithAcceptCounterOffer))]
    [HarmonyPrefix]
    private static bool Accept(IssueBase __instance) => Before(__instance, ArtisanProductsLordAction.AcceptMerchant);

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithLordSolutionWithRefuseCounterOffer))]
    [HarmonyPrefix]
    private static bool Refuse(IssueBase __instance) => Before(__instance, ArtisanProductsLordAction.RefuseMerchant);

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.BeforeGameMenuOpened))]
    [HarmonyPrefix]
    private static bool Menu(IssueBase __instance) => !(__instance is Issue) ||
        (ModInformation.IsClient && ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owner) &&
         ContainerProvider.TryResolve<IControllerIdProvider>(out var local) &&
         owner.TryGetOwnerControllerId(__instance, out var controller) && controller == local.ControllerId);

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.AddLog))]
    [HarmonyPostfix]
    private static void Journal(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance is Issue issue && issue.IsSolvingWithLordSolution &&
            ContainerProvider.TryResolve<ArtisanProductsLordHandler>(out var handler)) handler.SendJournal(issue);
    }

    private static bool Before(IssueBase issue, ArtisanProductsLordAction action)
    {
        if (!(issue is Issue artisan) || ModInformation.IsServer || AllowedThread.IsThisThreadAllowed()) return true;
        MessageBroker.Instance.Publish(issue, new ArtisanProductsLordRequested(artisan, action));
        return false;
    }
}

[HarmonyPatch]
internal class ArtisanProductsCounterOfferPatch
{
    [HarmonyTargetMethod]
    private static MethodBase Target() => AccessTools.Method(typeof(IssuesCampaignBehavior), "GetCounterOfferersIssue");

    [HarmonyPrefix]
    private static bool Prefix(ref IssueBase __result)
    {
        if (ModInformation.IsServer) return true;
        __result = FindCounterOffer();
        return false;
    }

    internal static IssueBase? FindCounterOffer()
    {
        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) ||
            !ContainerProvider.TryResolve<IControllerIdProvider>(out var local)) return null;
        var merchant = Hero.OneToOneConversationHero;
        if (merchant == null) return null;
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            if (issue.CounterOfferHero != merchant || !issue.IsSolvingWithLordSolution) continue;
            if (issue is Issue artisan && (artisan.IssueOwner == null || artisan.IssueOwner.Issue != artisan)) continue;
            if (issue is Issue && (!owners.TryGetOwnerControllerId(issue, out var owner) || owner != local.ControllerId)) continue;
            return issue;
        }
        return null;
    }
}

[HarmonyPatch]
internal class ArtisanProductsCounterOfferConversationPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        foreach (var name in new[]
        {
            "issue_counter_offer_2_condition",
            "issue_counter_offer_player_accept_condition",
            "issue_counter_offer_player_reject_condition",
            "issue_counter_offer_accepted_condition",
            "issue_counter_offer_reject_condition",
            "issue_counter_offer_accepted_consequence",
            "issue_counter_offer_reject_consequence",
        })
        {
            var method = AccessTools.Method(typeof(IssuesCampaignBehavior), name);
            if (method != null) yield return method;
        }
    }

    [HarmonyPrefix]
    private static bool Prefix()
        => ModInformation.IsServer || ArtisanProductsCounterOfferPatch.FindCounterOffer() != null;
}
