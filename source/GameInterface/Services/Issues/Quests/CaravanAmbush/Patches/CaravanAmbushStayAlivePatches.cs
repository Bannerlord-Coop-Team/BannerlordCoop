using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssue;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class CaravanAmbushStayAlivePatches
{
    // The dedicated server has no player clan; the accept preconditions check war as the requesting player.
    [HarmonyPatch(typeof(Issue), nameof(Issue.IssueStayAliveConditions))]
    [HarmonyPrefix]
    private static bool StayAlive(Issue __instance, ref bool __result)
    {
        __result = __instance.IssueOwner != null && __instance.IssueOwner.OwnedCaravans.Count > 0;
        return false;
    }
}
