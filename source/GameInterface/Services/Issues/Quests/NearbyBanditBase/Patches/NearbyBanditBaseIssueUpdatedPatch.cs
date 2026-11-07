using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Patches;

[HarmonyPatch(typeof(NearbyBanditBaseIssueBehavior), nameof(NearbyBanditBaseIssueBehavior.OnIssueUpdated))]
internal class NearbyBanditBaseIssueUpdatedPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}
