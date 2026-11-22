using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(NearbyBanditBaseIssueBehavior), nameof(NearbyBanditBaseIssueBehavior.OnIssueUpdated))]
internal class NearbyBanditBaseIssueUpdatedPatch
{
    // after an AI lord finishes the issue vanilla sends the hideout's parties back to patrol, the server owns that movement
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}
