using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

[HarmonyPatch(typeof(IssueBase))]
internal sealed class ArtisanProductEligibilityPatches
{
    [HarmonyPatch("get_IssueQuestCanBeDuplicated")]
    [HarmonyPostfix]
    private static void AllowOtherPlayers(IssueBase __instance, ref bool __result)
    {
        if (__instance is Issue) __result = true;
    }

    [HarmonyPatch(nameof(IssueBase.CheckPreconditions))]
    [HarmonyPostfix]
    private static void CheckOwnedIssues(IssueBase __instance, ref bool __result, ref TextObject explanation)
    {
        if (!__result || __instance is not Issue issue) return;
        if (ContainerProvider.TryResolve<IArtisanProductQuestAcceptance>(out var acceptance) &&
            !acceptance.HasConflictingIssue(issue)) return;
        __result = false;
        explanation = new TextObject("{=HvY7wjHt}I don't think you can help me. I think you may have other, similar commitments that could interfere.");
    }
}
