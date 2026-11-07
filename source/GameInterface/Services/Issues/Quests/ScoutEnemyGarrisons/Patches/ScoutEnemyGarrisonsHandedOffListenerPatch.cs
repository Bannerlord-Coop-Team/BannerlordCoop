using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ScoutEnemyGarrisonsHandedOffListenerPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.HourlyTick));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnSettlementOwnerChanged));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
    }

    // Otherwise every hour logs and requests the same ending again until the server answers
    [HarmonyPrefix]
    internal static bool Prefix(Quest __instance) => !ScoutEnemyGarrisonsBranchPatches.IsHandedOff(__instance);
}
