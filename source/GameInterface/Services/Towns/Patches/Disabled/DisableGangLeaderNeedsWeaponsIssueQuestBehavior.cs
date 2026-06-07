using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior))]
internal class DisableGangLeaderNeedsWeaponsIssueQuestBehavior
{
    [HarmonyPatch(nameof(GangLeaderNeedsWeaponsIssueQuestBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
