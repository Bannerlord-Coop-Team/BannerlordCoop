using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Runtime.CompilerServices;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Patches;

[HarmonyPatch]
internal class ScoutEnemyGarrisonsBranchPatches
{
    // Quests whose finished scouting this client already handed to the server
    private static readonly ConditionalWeakTable<Quest, object> Reported = new ConditionalWeakTable<Quest, object>();

    [HarmonyPatch(typeof(Quest), nameof(Quest.AllScoutingDone))]
    [HarmonyPrefix]
    private static bool AllScoutingDonePrefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        // The quest keeps ticking until the server's outcome arrives, the hourly tick must not report it again
        if (Reported.TryGetValue(__instance, out _))
        {
            return false;
        }

        if (dispatcher.BeforeQuestBranch(__instance, (byte)ScoutEnemyGarrisonsBranch.AllScoutingDone))
        {
            return true;
        }

        Reported.Add(__instance, new object());

        // The server grants the rewards, the owner's journal still gets vanilla's success entry
        __instance.AddLog(__instance.QuestSuccess);
        return false;
    }
}
