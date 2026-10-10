using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

[HarmonyPatch(typeof(DefaultIssueModel), nameof(DefaultIssueModel.GetIssueDifficultyMultiplier))]
internal class IssueDifficultyOverridePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref float __result)
    {
        if (!IssueDifficultyOverride.TryGet(out var multiplier))
        {
            return true;
        }

        __result = multiplier;
        return false;
    }
}
