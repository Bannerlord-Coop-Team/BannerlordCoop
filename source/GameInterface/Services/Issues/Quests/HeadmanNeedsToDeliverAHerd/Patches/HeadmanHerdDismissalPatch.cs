using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class HeadmanHerdDismissalPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase __instance)
    {
        if (ModInformation.IsClient || !(__instance is Issue)) return;
        // Startup dismissal has no ownership registry, so the saved original party owns this cleanup.
        if (ContainerProvider.TryResolve<IHeadmanHerdCleanup>(out var cleanup)) cleanup.Finalize(__instance);
    }
}
