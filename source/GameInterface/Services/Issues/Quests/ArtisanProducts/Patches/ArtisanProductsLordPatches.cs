using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts.Patches;

[HarmonyPatch]
internal class ArtisanProductsLordPatches
{
    [HarmonyPatch]
    [HarmonyPrefix]
    private static bool CounterOffer(ref IssueBase __result)
    {
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<IIssueOwnerResolver>(out var owners) ||
            !ContainerProvider.TryResolve<IControllerIdProvider>(out var local)) return true;
        __result = null;
        var merchant = Hero.OneToOneConversationHero;
        if (merchant == null) return false;
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            if (issue.CounterOfferHero != merchant || !issue.IsSolvingWithLordSolution) continue;
            if (issue is Issue && (!owners.TryGetOwnerControllerId(issue, out var owner) || owner != local.ControllerId)) continue;
            __result = issue;
            break;
        }
        return false;
    }

    [HarmonyTargetMethod]
    private static MethodBase CounterOfferTarget() => AccessTools.Method(typeof(IssuesCampaignBehavior), "GetCounterOfferersIssue");

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
