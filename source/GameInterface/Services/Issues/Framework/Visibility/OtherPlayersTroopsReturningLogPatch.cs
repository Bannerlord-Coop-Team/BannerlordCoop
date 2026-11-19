using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Visibility;

/// <summary>
/// Vanilla's daily tick tells the troops' owner they are on their way back. On another player's client that
/// log would raise an unread quest badge for a mission the quest screen hides, so it can never be cleared.
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
