using Common;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class CaravanAmbushDialogPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.SetDialogs))]
    [HarmonyPrefix]
    private static bool Dialogs() => ModInformation.IsClient;
}
