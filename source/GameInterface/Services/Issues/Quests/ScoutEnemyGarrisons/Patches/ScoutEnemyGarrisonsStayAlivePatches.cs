using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Messages;
using HarmonyLib;
using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Patches;

/// <summary>
/// Vanilla rerolls an offered issue's targets at random when they stop being enemies, and the conversation's
/// precondition check cancels the issue when that fails, which on a client removed the offer on that machine only.
/// The server alone rerolls or ends the issue and sends the new targets to the clients.
/// </summary>
[HarmonyPatch]
internal class ScoutEnemyGarrisonsStayAlivePatches
{
    [HarmonyPatch(typeof(Issue), nameof(Issue.IssueStayAliveConditions))]
    [HarmonyPrefix]
    private static bool IssueStayAliveConditionsPrefix(ref bool __result)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        __result = true;
        return false;
    }

    [HarmonyPatch(typeof(Issue), nameof(Issue.TryToUpdateSettlements))]
    [HarmonyPostfix]
    internal static void TryToUpdateSettlementsPostfix(Issue __instance, bool __result)
    {
        if (ModInformation.IsServer && __result)
        {
            MessageBroker.Instance.Publish(__instance, new ScoutEnemyGarrisonsTargetsChanged(__instance));
        }
    }
}
