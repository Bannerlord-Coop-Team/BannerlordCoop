using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordNeedsGarrisonTroops.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.PlayerTransferredTroopsToGarrisonCommander))]
internal class LordNeedsGarrisonTroopsHandInPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeQuestBranch(__instance, (byte)LordNeedsGarrisonTroopsBranch.TroopsDelivered);
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess))]
internal class LordNeedsGarrisonTroopsSuccessJournalPatch
{
    [HarmonyPrefix]
    private static void Prefix(QuestBase __instance)
    {
        if (ModInformation.IsClient && CallOriginalPolicy.IsOriginalAllowed() && __instance is Quest quest)
        {
            quest.AddLog(quest.SuccessQuestLogText);
        }
    }
}
