using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts a genuine server-side <c>IssueManager.CreateNewIssue</c> creating an
/// <see cref="ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue"/> - see
/// <see cref="IArtisanCantSellProductsAtAFairPriceIssueInterface"/>'s doc comment. Deliberately its own
/// independent postfix (same reasoning as e.g. <see cref="HeadmanNeedsToDeliverAHerdIssueCreationPatch"/>): the
/// client-creation-blocking Prefix on <see cref="IssueManagerCreateNewIssuePatches"/> is already fully generic.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class ArtisanCantSellProductsAtAFairPriceIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue) return;

        MessageBroker.Instance.Publish(issueOwner, new ArtisanCantSellProductsAtAFairPriceIssueCreated(issue));
    }
}
