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
    // The server adds the mercenaries to the owner's party, so the owner's client keeps only vanilla's log and notice
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
