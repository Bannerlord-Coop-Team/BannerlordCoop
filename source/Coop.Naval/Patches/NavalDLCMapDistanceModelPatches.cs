using HarmonyLib;
using NavalDLC.GameComponents;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.Patches;

/// <summary>
/// Guards NavalDLCMapDistanceModel against navigation caches that are not registered yet.
/// Naval counterpart of the GameInterface MapDistance null cache guards. A joining client loads the save before the
/// map scene registers its caches, and these methods index <c>_navigationCaches</c> directly so they throw.
/// </summary>
[HarmonyPatch(typeof(NavalDLCMapDistanceModel))]
internal class NavalDLCMapDistanceModelPatches
{
    [HarmonyPatch(nameof(NavalDLCMapDistanceModel.GetDistance),
        new[] { typeof(Settlement), typeof(Settlement), typeof(bool), typeof(bool), typeof(MobileParty.NavigationType), typeof(float) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
    [HarmonyPrefix]
    private static bool GetDistancePrefix(NavalDLCMapDistanceModel __instance, Settlement fromSettlement, Settlement toSettlement, MobileParty.NavigationType navigationCapability, ref float landRatio, ref float __result)
    {
        // Only the distinct settlement path reads the cache
        if (fromSettlement == null || toSettlement == null || fromSettlement == toSettlement) return true;
        if (__instance._navigationCaches.ContainsKey(navigationCapability)) return true;

        landRatio = 1f;
        __result = fromSettlement.GatePosition.Distance(toSettlement.GatePosition);
        return false;
    }

    [HarmonyPatch(nameof(NavalDLCMapDistanceModel.GetClosestEntranceToFace))]
    [HarmonyPrefix]
    private static bool GetClosestEntranceToFacePrefix(NavalDLCMapDistanceModel __instance, MobileParty.NavigationType navigationCapabilities, ref ValueTuple<Settlement, bool> __result)
    {
        if (__instance._navigationCaches.ContainsKey(navigationCapabilities)) return true;

        __result = (null, false);
        return false;
    }

    [HarmonyPatch(nameof(NavalDLCMapDistanceModel.GetPortToGateDistanceForSettlement))]
    [HarmonyPrefix]
    private static bool GetPortToGateDistanceForSettlementPrefix(NavalDLCMapDistanceModel __instance, Settlement settlement, ref float __result)
    {
        if (__instance._navigationCaches.ContainsKey(MobileParty.NavigationType.All)) return true;

        __result = settlement.PortPosition.Distance(settlement.GatePosition);
        return false;
    }
}
