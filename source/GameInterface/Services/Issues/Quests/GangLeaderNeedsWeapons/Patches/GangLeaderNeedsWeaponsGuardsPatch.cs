using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons.Patches;

/// <summary>
/// The guards at the gate need a guards party that only the server can create, and what they take from the
/// owner (weapons, bribe gold) has to change the owner's inventory and gold on the server while the quest is
/// still running. The framework only runs quest outcomes on the server, so for now the guards do not appear
/// and the player delivers the weapons directly.
/// </summary>
[HarmonyPatch(typeof(Quest), "OnSettlementEnter")]
internal class GangLeaderNeedsWeaponsGuardsPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return false;
    }
}
