using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch]
internal sealed class ArtisanProductIssueCreationPatches
{
    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Created(Hero issueOwner, bool __result)
    {
        if (ModInformation.IsServer && __result &&
            issueOwner?.Issue is ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue)
        {
            MessageBroker.Instance.Publish(issueOwner, new ArtisanProductIssueCreated(issue));
        }
    }

    [HarmonyPatch(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior), "SelectTargetSettlement")]
    [HarmonyPrefix]
    private static bool SelectReplicatedDestination(ref Settlement __result, out IDisposable __state)
    {
        __state = null;
        if (ArtisanProductDestinationScope.Destination != null)
        {
            __result = ArtisanProductDestinationScope.Destination;
            return false;
        }
        if (ModInformation.IsClient) return true;
        if (ContainerProvider.TryResolve<IArtisanProductGenerationContext>(out var context) &&
            context.TryEnter(out __state)) return true;
        __result = null;
        return false;
    }

    [HarmonyPatch(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior), "SelectTargetSettlement")]
    [HarmonyFinalizer]
    private static void RestoreGenerationContext(IDisposable __state) => __state?.Dispose();
}
