using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
internal class MapEventDestructionPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MapEventDestructionPatches>();

    [HarmonyPatch(nameof(MapEvent.FinalizeEvent))]
    static bool Prefix()
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(MapEvent.FinalizeEvent))]
    static void Postfix(MapEvent __instance)
    {
        // Call original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
            return;
        }

        var message = new MapEventDestroyed(__instance);

        MessageBroker.Instance.Publish(__instance, message);
    }
}
