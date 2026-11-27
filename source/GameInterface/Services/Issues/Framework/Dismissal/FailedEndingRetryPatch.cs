using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dismissal;

/// <summary>
/// An issue whose ending threw has no owner entry left, so the server's daily tick only skips it. After that
/// tick it is taken out of the manager without running its ending again, so the clients still hear about it.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class FailedEndingRetryPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IActiveIssueDismisser>(out var dismisser)) return;

        dismisser.RetryFailedEndings();
    }
}
