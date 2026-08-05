using Common;
using Common.Util;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Applies each replicated besieger-camp write to that party only. The server emits a reliable ordered
/// property update for every party it removes, so repeating vanilla's attached-party cascade on receipt
/// can remove followers before their own authoritative update arrives.
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.BesiegerCamp), MethodType.Setter)]
internal static class MobilePartyBesiegerCampCascadePatch
{
    [HarmonyPrefix]
    private static void Prefix(MobileParty __instance, out MBList<MobileParty> __state)
    {
        __state = null;
        if (!ModInformation.IsClient || !AllowedThread.IsThisThreadAllowed()) return;
        if (__instance._attachedParties == null || __instance._attachedParties.Count == 0) return;

        __state = __instance._attachedParties;
        __instance._attachedParties = new MBList<MobileParty>();
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(
        MobileParty __instance,
        MBList<MobileParty> __state,
        Exception __exception)
    {
        if (__state != null)
            __instance._attachedParties = __state;

        return __exception;
    }
}
