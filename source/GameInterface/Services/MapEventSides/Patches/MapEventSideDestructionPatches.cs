using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventSides.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEventSide))]
internal class MapEventSideDestructionPatches
{
    static readonly ILogger Logger = LogManager.GetLogger<MapEventSideDestructionPatches>();

    [HarmonyPatch(nameof(MapEventSide.HandleMapEventEnd))]
    static void Postfix(MapEventSide __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEventSide), Environment.StackTrace);
            return;
        }

        var message = new MapEventSideDestroyed(__instance);

        MessageBroker.Instance.Publish(__instance, message);
    }
}
