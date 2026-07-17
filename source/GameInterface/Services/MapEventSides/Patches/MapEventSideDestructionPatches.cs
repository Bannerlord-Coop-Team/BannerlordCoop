using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventParties.Messages;
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
    [HarmonyPriority(Priority.First)]
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

        // Flush before removal detaches the party from the side. Once the last party leaves,
        // finalization can no longer discover its pending contribution through the map-event graph.
        if (ModInformation.IsServer)
            MessageBroker.Instance.Publish(__instance,
                new MapEventContributionFlushRequested(mapEventParty));

        __instance.InvalidateSimulationSetup();
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

            // Skip when the event is already finalizing: FinalizeEventAux empties the sides through
            // HandleMapEventEnd, so this last-party removal re-enters here mid-finalize. Re-finalizing then
            // is a no-op vanilla guards with IsFinalized, but it re-runs the finalize side effects. Only
            // finalize when this removal is what ends the battle.
            if (!__instance.MapEvent.IsFinalized)
            {
                __instance.MapEvent.FinalizeEvent();
            }
        }

        return false;
    }
}
