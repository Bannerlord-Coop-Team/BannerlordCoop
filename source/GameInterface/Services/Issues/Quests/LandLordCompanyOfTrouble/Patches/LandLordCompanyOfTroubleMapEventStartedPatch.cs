using Common;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class LandLordCompanyOfTroubleMapEventStartedPatch
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool OnMapEventStartedPrefix()
    {
        return ModInformation.IsServer;
    }
}
