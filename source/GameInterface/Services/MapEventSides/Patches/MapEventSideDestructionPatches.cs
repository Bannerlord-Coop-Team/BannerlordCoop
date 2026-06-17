using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.LinQuick;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEventSide))]
internal class MapEventSideDestructionPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MapEventSideDestructionPatches>();

    [HarmonyPatch(nameof(MapEventSide.RemovePartyInternal))]
    [HarmonyPrefix]
    static bool Prefix(MapEventSide __instance, PartyBase party)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        int index = __instance._battleParties.FindIndexQ((MapEventParty p) => p.Party == party);

        if (index == -1)
        {
            Logger.Error("Could not find {party} in {var}", party.Name, nameof(MapEventSide._battleParties));
            return false;
        }

        MapEventParty mapEventParty = __instance._battleParties[index];
        __instance._battleParties.RemoveAt(index);
        __instance._mapEvent.RemoveInvolvedPartyInternal(mapEventParty);
        if (__instance.LeaderParty == party)
        {
            __instance._mapFaction = __instance.LeaderParty.MapFaction;
            if (__instance._battleParties.Count > 0)
            {
                __instance.LeaderParty = __instance._battleParties[0].Party;
                __instance._mapFaction = __instance.LeaderParty.MapFaction;
                __instance.CacheLeaderSimulationModifier();
                return false;
            }
            __instance.MapEvent.FinalizeEvent();
        }

        return false;
    }
}
