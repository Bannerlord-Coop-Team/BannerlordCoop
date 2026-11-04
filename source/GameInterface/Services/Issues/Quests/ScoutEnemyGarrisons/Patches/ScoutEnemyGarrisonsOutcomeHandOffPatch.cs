using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ScoutEnemyGarrisonsOutcomeHandOffPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut));
    }

    [HarmonyPostfix]
    internal static void Postfix(QuestBase __instance)
    {
        // Still running after an ending means the dispatcher handed the ending to the server instead
        if (ModInformation.IsClient && __instance is Quest quest && quest.IsOngoing)
        {
            ScoutEnemyGarrisonsBranchPatches.HandOff(quest);
        }
    }
}
