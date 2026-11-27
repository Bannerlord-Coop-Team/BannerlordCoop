using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>
/// Every server troop return would activate a sent companion whose death already ran and throw, so such companions
/// stay behind first, and the return is noted on the issue's claim. A quest prefix that replaces the return may skip
/// this one, so it runs both steps itself.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.TryToMakeTroopsReturn))]
internal class AlternativeTroopsReturnPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase issue)
    {
        if (ModInformation.IsClient) return;

        if (ContainerProvider.TryResolve<IIssueEndingClaimRegistry>(out var claims)) claims.NoteTroopReturn(issue);
        if (ContainerProvider.TryResolve<IIssueDismisser>(out var issueDismisser)) issueDismisser.LeaveBehindDeadCompanions(issue);
    }
}
