using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty), (nameof(MobileParty.RecalculateShortTermBehavior)))]
class TempRecalculatePatch
{
    private static ILogger Logger = LogManager.GetLogger<GameLoopRunner>();

    [HarmonyPrefix]
    private static bool Prefix(MobileParty __instance)
    {
        if (__instance.DefaultBehavior == AiBehavior.RaidSettlement)
        {
            if (__instance.TargetSettlement is null) return false;
            __instance.SetShortTermBehavior(AiBehavior.RaidSettlement, __instance.TargetSettlement.Party);
            return false;
        }
        if (__instance.DefaultBehavior == AiBehavior.BesiegeSettlement)
        {
            if (__instance.TargetSettlement is null) return false;
            __instance.SetShortTermBehavior(AiBehavior.BesiegeSettlement, __instance.TargetSettlement.Party);
            return false;
        }
        if (__instance.DefaultBehavior == AiBehavior.GoToSettlement)
        {
            if (__instance.TargetSettlement is null) return false;
            __instance.SetShortTermBehavior(AiBehavior.GoToSettlement, __instance.TargetSettlement.Party);
            return false;
        }
        if (__instance.DefaultBehavior == AiBehavior.EngageParty)
        {
            if (__instance.TargetParty is null) return false;
            __instance.SetShortTermBehavior(AiBehavior.EngageParty, __instance.TargetParty.Party);
            return false;
        }
        if (__instance.DefaultBehavior == AiBehavior.DefendSettlement)
        {
            if (__instance.TargetSettlement is null) return false;
            __instance.SetShortTermBehavior(AiBehavior.GoToPoint, __instance.TargetSettlement.Party);
            return false;
        }
        if (__instance.DefaultBehavior == AiBehavior.EscortParty)
        {
            if (__instance.TargetParty is null) return false;
            __instance.SetShortTermBehavior(AiBehavior.EscortParty, __instance.TargetParty.Party);
            return false;
        }
        if (__instance.DefaultBehavior == AiBehavior.GoToPoint)
        {
            __instance.SetShortTermBehavior(AiBehavior.GoToPoint, __instance.Ai.AiBehaviorInteractable);
            return false;
        }
        if (__instance.DefaultBehavior == AiBehavior.MoveToNearestLandOrPort)
        {
            __instance.SetShortTermBehavior(AiBehavior.GoToPoint, null);
            return false;
        }
        if (__instance.DefaultBehavior == AiBehavior.None)
        {
            __instance.ShortTermBehavior = AiBehavior.None;
        }

        return false;
    }
}
