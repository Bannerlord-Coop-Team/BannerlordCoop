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
        if (CallPolicy.IsOriginalAllowed()) return;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        var message = new MapEventSideDestroyed(__instance);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(__instance, message);
    }
}
