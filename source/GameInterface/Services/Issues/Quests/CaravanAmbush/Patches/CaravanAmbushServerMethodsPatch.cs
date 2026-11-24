using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class CaravanAmbushServerMethodsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.HourlyTick));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.MapEventStarted));
    }

    private static bool Prefix() => ModInformation.IsServer;
}
