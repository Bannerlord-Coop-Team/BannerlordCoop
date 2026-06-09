using Common;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
public class MapEventUpdatePatch
{
    [HarmonyPrefix]
    [HarmonyPatch("Update")]
    static bool PrefixUpdate(MapEvent __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
            return true;

        if (ModInformation.IsClient)
            return false;

        // Skip if any parties are not set
        if (__instance.InvolvedParties.Any(x => x?.MobileParty is null))
            return false;

        // Don't update if a player is involved
        // Prevents server from instantly finishing the battle and waits for client finish request
        if (__instance.InvolvedParties.Any(x => !x.MobileParty.IsControlledByThisInstance()))
            return false;

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MapEvent.Initialize))]
    static bool PrefixInitialize(MapEvent __instance, PartyBase attackerParty, PartyBase defenderParty, MapEventComponent component, MapEvent.BattleTypes mapEventType)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Only run on server
        return ModInformation.IsServer;
    }
}