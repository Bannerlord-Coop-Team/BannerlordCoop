using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Patches;

/// <summary>
/// Vanilla skips a target besieged by the player's faction. The dedicated server's main hero is not a player,
/// and only a player of the issue owner's faction can take the quest, so the server compares to that faction.
/// </summary>
[HarmonyPatch(typeof(ScoutEnemyGarrisonsIssueBehavior), nameof(ScoutEnemyGarrisonsIssueBehavior.SuitableSettlementCondition))]
internal class ScoutEnemyGarrisonsTargetPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Settlement settlement, Hero issueGiver, ref bool __result)
    {
        if (ModInformation.IsClient)
        {
            return true;
        }

        __result = settlement.IsFortification &&
            settlement.MapFaction.IsAtWarWith(issueGiver.MapFaction) &&
            (!settlement.IsUnderSiege || settlement.SiegeEvent.BesiegerCamp.MapFaction != issueGiver.MapFaction);

        return false;
    }
}
