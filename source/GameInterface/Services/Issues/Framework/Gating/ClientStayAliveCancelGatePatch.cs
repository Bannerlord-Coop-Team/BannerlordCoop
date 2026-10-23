using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Gating;

/// <summary>
/// Vanilla cancels an issue that fails its stay-alive conditions while a player checks whether they can take it.
/// On a client that removes the issue only there, so the client refuses without cancelling and leaves the cancel
/// to the server's daily stay-alive check.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CheckPreconditions))]
internal class ClientStayAliveCancelGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, out TextObject explanation, ref bool __result)
    {
        explanation = null;
        if (ModInformation.IsServer || !__instance.IsOngoingWithoutQuest || __instance.IssueStayAliveConditions())
        {
            return true;
        }

        explanation = new TextObject("{=!}{EXPLANATION}");
        __result = false;
        return false;
    }
}
