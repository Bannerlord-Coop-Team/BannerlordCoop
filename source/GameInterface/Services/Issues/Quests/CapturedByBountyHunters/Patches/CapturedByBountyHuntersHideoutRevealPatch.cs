using Common;
using Common.Util;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters.Patches;

// Only the owner's client runs the quest, so the hideout its scouts mark stays on that player's map
[HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementLeft))]
internal class CapturedByBountyHuntersHideoutRevealPatch
{
    [HarmonyPrefix]
    private static void Prefix(out AllowedThread __state)
    {
        __state = ModInformation.IsClient ? new AllowedThread() : null;
    }

    [HarmonyFinalizer]
    private static void Finalizer(AllowedThread __state)
    {
        __state?.Dispose();
    }
}
