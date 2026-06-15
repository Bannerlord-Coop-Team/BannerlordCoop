using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEventParties.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Patches;

[HarmonyPatch(typeof(MapEventParty))]
internal class MapEventPartyUpdatePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventPartyUpdatePatch>();

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MapEventParty.Update))]
    static bool PrefixUpdate(MapEventParty __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        return ModInformation.IsServer;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(MapEventParty.Update))]
    static void PostfixUpdate(MapEventParty __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
            return;

        // The full flattened roster is only needed by clients when a player party is in the battle
        // (to spawn the troops in the mission). Pure AI battles are simulated entirely server-side, so
        // skip the large NetworkUpdateMapEventParty broadcast for them.
        if (__instance.Party?.MapEvent.ContainsPlayerParty() != true)
            return;

        var message = new MapEventPartyUpdated(__instance, __instance._roster);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
