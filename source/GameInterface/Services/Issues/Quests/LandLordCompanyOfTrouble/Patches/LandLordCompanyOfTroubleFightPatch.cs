using Common;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
internal class LandLordCompanyOfTroubleFightPatch
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.CreateCompanyEnemyParty))]
    [HarmonyPrefix]
    private static bool CreateCompanyEnemyPartyPrefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        __instance._companyLeftQuestWillFail = true;
        return false;
    }
}
