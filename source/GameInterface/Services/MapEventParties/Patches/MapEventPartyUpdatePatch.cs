using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
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

        var message = new MapEventPartyUpdated(__instance, __instance._roster);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
