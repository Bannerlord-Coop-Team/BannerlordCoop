using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior))]
internal class DisableGangLeaderNeedsWeaponsIssueQuestBehavior
{
    [HarmonyPatch(nameof(GangLeaderNeedsWeaponsIssueQuestBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
