using Common;
using Common.Util;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssueQuest;

namespace GameInterface.Services.Issues.Quests.CapturedByBountyHunters.Patches;

// The hideout's spotted state is server managed, the owner's client marks its quest hideout for itself on purpose
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
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
