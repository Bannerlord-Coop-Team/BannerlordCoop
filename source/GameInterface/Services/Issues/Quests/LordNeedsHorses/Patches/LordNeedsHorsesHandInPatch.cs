using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordNeedsHorses.Patches;

/// <summary>
/// Vanilla only checks the mounts in the hand-in dialog, which is the owner's machine. The server runs the
/// success as the owning player and refuses it when that player does not carry the mounts.
/// </summary>
[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess))]
internal class LordNeedsHorsesHandInPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordNeedsHorsesHandInPatch>();

    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance)
    {
        if (ModInformation.IsClient || !(__instance is Quest quest))
        {
            return true;
        }

        var carried = quest.GetNumQuestMountsInInventory();
        if (carried >= quest._numMountsToBeDelivered)
        {
            return true;
        }

        Logger.Warning("The owner of {quest} carries {carried} of the {needed} mounts, not completing it", quest.StringId, carried, quest._numMountsToBeDelivered);
        return false;
    }
}
