using Common;
using HarmonyLib;
using Helpers;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Patches;

[HarmonyPatch(typeof(Quest), nameof(Quest.OnQuestAccepted))]
internal class NearbyBanditBaseQuestAcceptedPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        var hideout = __instance._targetHideout;

        __instance.StartQuest();
        hideout.IsVisible = true;
        __instance.AddTrackedObject(hideout);
        QuestHelper.AddMapArrowFromPointToTarget(new TextObject("{=xpsQyPaV}Direction to Bandits"), __instance._questSettlement.Position, hideout.Position, 5f, 0.1f);

        var markedText = new TextObject("{=XGa8MkbJ}{QUEST_GIVER.NAME} has marked the hideout on your map");
        StringHelpers.SetCharacterProperties("QUEST_GIVER", __instance.QuestGiver.CharacterObject, markedText);
        MBInformationManager.AddQuickInformation(markedText);

        __instance.AddLog(__instance.OnQuestStartedLogText);

        return false;
    }
}
