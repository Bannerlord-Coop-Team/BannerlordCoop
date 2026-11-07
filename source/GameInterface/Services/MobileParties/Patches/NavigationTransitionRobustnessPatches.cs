using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.FinishNavigationTransitionInternal))]
internal class NavigationTransitionRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavigationTransitionRobustnessPatches>();

    // A settlement leave or army change applied mid-transition clears the end position, so finishing would move the party to NaN
    [HarmonyPrefix]
    private static bool Prefix(MobileParty __instance)
    {
        if (__instance.CurrentSettlement != null) return true;

        var endPosition = __instance.Army != null && __instance.AttachedTo != null
            ? __instance.Army.LeaderParty.EndPositionForNavigationTransition
            : __instance.EndPositionForNavigationTransition;
        if (endPosition.ToVec2().IsValid) return true;

        Logger.Warning("Cancelling the navigation transition of {PartyId} because it has no end position", __instance.StringId);
        __instance.CancelNavigationTransition();
        return false;
    }
}
