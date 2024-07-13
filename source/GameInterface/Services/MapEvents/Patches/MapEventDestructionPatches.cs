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
    static void Postfix(MapEvent __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
            return;
        }

        var message = new MapEventCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);
    }
}
