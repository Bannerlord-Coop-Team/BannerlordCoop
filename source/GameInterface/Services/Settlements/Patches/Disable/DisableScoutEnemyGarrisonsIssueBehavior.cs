using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(ScoutEnemyGarrisonsIssueBehavior))]
internal class DisableScoutEnemyGarrisonsIssueBehavior
{
    [HarmonyPatch(nameof(ScoutEnemyGarrisonsIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
