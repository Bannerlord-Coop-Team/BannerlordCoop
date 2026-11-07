using Common;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Patches;

[HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
internal class NearbyBanditBaseWarDeclaredPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}
