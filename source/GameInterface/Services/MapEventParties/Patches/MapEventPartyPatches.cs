using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventParties.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventParties.Patches;

[HarmonyPatch(typeof(MapEventParty))]
internal class MapEventPartyPatches
{
    [HarmonyPatch(nameof(MapEventParty.OnTroopKilled))]
    [HarmonyPrefix]
    private static bool PrefixOnTroopKilled(MapEventParty __instance, ref UniqueTroopDescriptor troopSeed)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsServer) return true;

        __instance._roster.OnTroopKilled(troopSeed);

        var troop = __instance._roster[troopSeed].Troop;
        MessageBroker.Instance.Publish(__instance, new OnTroopKilledAttempted(__instance, troop));

        return false;
    }

    [HarmonyPatch(nameof(MapEventParty.OnTroopWounded))]
    [HarmonyPrefix]
    private static bool PrefixOnTroopWounded(MapEventParty __instance, ref UniqueTroopDescriptor troopSeed)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsServer) return true;

        __instance._roster.OnTroopWounded(troopSeed);

        var troop = __instance._roster[troopSeed].Troop;
        MessageBroker.Instance.Publish(__instance, new OnTroopWoundedAttempted(__instance, troop));

        return false;
    }
}
