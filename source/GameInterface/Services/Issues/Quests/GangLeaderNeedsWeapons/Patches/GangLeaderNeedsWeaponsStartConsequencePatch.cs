using Common;
using HarmonyLib;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons.Patches;

[HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionStartConsequence))]
internal class GangLeaderNeedsWeaponsStartConsequencePatch
{
    // The server charges the owner once after the accept has passed validation.
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}
