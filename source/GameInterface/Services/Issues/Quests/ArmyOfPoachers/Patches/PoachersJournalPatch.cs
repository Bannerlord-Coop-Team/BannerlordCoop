using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers.Patches;

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
internal class PoachersJournalPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, TextObject text)
    {
        if (__instance is Quest && ContainerProvider.TryResolve<PoachersJournalHandler>(out var journals))
            journals.SendLog(__instance, text);
    }
}
