using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class LandLordCompanyOfTroubleCompanyDialogPatch
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.CompanyDialogFromCondition))]
    [HarmonyPostfix]
    private static void CompanyDialogFromConditionPostfix(ref bool __result)
    {
        if (__result && MobileParty.ConversationParty != MobileParty.MainParty)
        {
            __result = false;
        }
    }
}
