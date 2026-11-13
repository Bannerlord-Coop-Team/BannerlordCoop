using Common;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class LandLordCompanyOfTroubleAcceptPatch
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
    [HarmonyPrefix]
    private static bool QuestAcceptedConsequencesPrefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        __instance.StartQuest();
        __instance.AddLog(__instance.PlayerStartsQuestLogText);
        MBInformationManager.AddQuickInformation(new TextObject("{=jGIxKb99}Mercenaries have joined your party."));
        return false;
    }
}
