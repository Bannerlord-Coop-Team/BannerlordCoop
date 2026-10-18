using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordNeedsGarrisonTroops.Patches;

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
