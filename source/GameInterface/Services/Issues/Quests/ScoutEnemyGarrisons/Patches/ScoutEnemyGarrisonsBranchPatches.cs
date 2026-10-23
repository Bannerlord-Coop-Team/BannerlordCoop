using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Runtime.CompilerServices;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Patches;

[HarmonyPatch]
internal class ScoutEnemyGarrisonsBranchPatches
{
    // Quests whose ending this client already handed to the server, they keep running until the server's outcome arrives
    private static readonly ConditionalWeakTable<Quest, object> HandedOff = new ConditionalWeakTable<Quest, object>();

    [HarmonyPatch(typeof(Quest), nameof(Quest.AllScoutingDone))]
    [HarmonyPrefix]
    private static bool AllScoutingDonePrefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        if (dispatcher.BeforeQuestBranch(__instance, (byte)ScoutEnemyGarrisonsBranch.AllScoutingDone))
        {
            return true;
        }

        HandOff(__instance);
        return false;
    }

    internal static bool IsHandedOff(Quest quest) => HandedOff.TryGetValue(quest, out _);

    internal static void HandOff(Quest quest)
    {
        if (!IsHandedOff(quest))
        {
            HandedOff.Add(quest, new object());
        }
    }
}
