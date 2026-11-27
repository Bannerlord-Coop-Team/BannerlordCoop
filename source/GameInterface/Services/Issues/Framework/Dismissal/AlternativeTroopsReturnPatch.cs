using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>
/// Every server troop return, vanilla's and the quests' own, would activate a sent companion whose death already ran
/// and throw, so such companions stay behind before any of them runs. Quest prefixes on this method that skip the
/// original must run after this one, so keep the priority first. It also notes the return on the issue's claim.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.TryToMakeTroopsReturn))]
internal class AlternativeTroopsReturnPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(IssueBase issue)
    {
        if (ModInformation.IsClient) return;

        if (ContainerProvider.TryResolve<IActiveIssueDismisser>(out var dismisser)) dismisser.NoteTroopReturn(issue);
        if (ContainerProvider.TryResolve<IIssueDismisser>(out var issueDismisser)) issueDismisser.LeaveBehindDeadCompanions(issue);
    }
}
