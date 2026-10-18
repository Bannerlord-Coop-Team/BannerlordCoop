using Common;
using HarmonyLib;
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;

namespace GameInterface.Services.Issues.Quests.LordNeedsHorses.Patches;

[HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionStartConsequence))]
internal class LordNeedsHorsesStartConsequencePatch
{
    // Vanilla takes the gold on the client before the server accepts, the server takes it when it does
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}
