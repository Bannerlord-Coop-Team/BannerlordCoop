using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

[HarmonyPatch(typeof(IssueBase))]
internal sealed class ArtisanProductLordActionPatches
{
    [HarmonyPatch(nameof(IssueBase.StartIssueWithLordSolution))]
    [HarmonyPrefix]
    private static bool Start(IssueBase __instance) => Request(__instance, ArtisanProductLordAction.Start);

    [HarmonyPatch(nameof(IssueBase.CompleteIssueWithLordSolutionWithAcceptCounterOffer))]
    [HarmonyPrefix]
    private static bool AcceptOffer(IssueBase __instance) => Request(__instance, ArtisanProductLordAction.AcceptMerchantOffer);

    [HarmonyPatch(nameof(IssueBase.CompleteIssueWithLordSolutionWithRefuseCounterOffer))]
    [HarmonyPrefix]
    private static bool RefuseOffer(IssueBase __instance) => Request(__instance, ArtisanProductLordAction.RefuseMerchantOffer);

    [HarmonyPatch("BeforeGameMenuOpened")]
    [HarmonyPrefix]
    private static bool OpenMenu(IssueBase __instance)
        => __instance is not Issue || (ModInformation.IsClient &&
            ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) &&
            ownership.IsLocalPeerOwner(__instance.IssueOwner));

    private static bool Request(IssueBase issue, ArtisanProductLordAction action)
    {
        if (issue is not Issue || ModInformation.IsServer) return true;
        MessageBroker.Instance.Publish(issue, new ArtisanProductLordActionRequested(issue.IssueOwner, action));
        return false;
    }
}

[HarmonyPatch(typeof(Issue), "OnGameLoad")]
internal sealed class ArtisanProductLordLoadPatch
{
    [HarmonyPostfix]
    private static void Loaded(Issue __instance)
    {
        if (!__instance.IsSolvingWithLordSolution) return;
        CampaignEvents.BeforeGameMenuOpenedEvent.ClearListeners(__instance);
        CampaignEvents.BeforeGameMenuOpenedEvent.AddNonSerializedListener(__instance, __instance.BeforeGameMenuOpened);
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), "GetCounterOfferersIssue")]
internal sealed class ArtisanProductLordCounterOfferSelectionPatch
{
    [HarmonyPrefix]
    private static bool SelectOwnedIssue(ref IssueBase __result)
    {
        if (Hero.OneToOneConversationHero == null) return true;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership)) return true;
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            if (issue.CounterOfferHero != Hero.OneToOneConversationHero || !issue.IsSolvingWithLordSolution) continue;
            if (issue is Issue && !ownership.IsLocalPeerOwner(issue.IssueOwner)) continue;
            __result = issue;
            return false;
        }
        __result = null;
        return false;
    }
}
