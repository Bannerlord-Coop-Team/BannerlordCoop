using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
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

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess))]
    [HarmonyPrefix]
    private static void ReplayedSuccessPrefix(QuestBase __instance)
    {
        // The server ran AllScoutingDone, the owner's journal gets vanilla's success entry when the outcome is replayed
        if (ModInformation.IsClient && __instance is Quest quest && CallOriginalPolicy.IsOriginalAllowed())
        {
            quest.AddLog(quest.QuestSuccess);
        }
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
