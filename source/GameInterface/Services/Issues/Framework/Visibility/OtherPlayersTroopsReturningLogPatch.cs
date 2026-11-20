using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>
/// Suppresses the hidden mission's return log when the client daily tick falls back to vanilla.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.OnAlternativeSolutionSolvedAndTroopsAreReturning))]
internal class OtherPlayersTroopsReturningLogPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<IIssueMissionVisibility>(out var visibility) ||
            !visibility.IsAnotherPlayersTroopsMission(__instance))
        {
            return true;
        }

        __instance._areIssueEffectsResolved = true;
        return false;
    }
}
