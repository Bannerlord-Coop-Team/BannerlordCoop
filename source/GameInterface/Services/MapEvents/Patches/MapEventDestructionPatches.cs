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
        if (CallPolicy.IsOriginalAllowed()) return true;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

        return true;
    }

    [HarmonyPatch(nameof(MapEvent.FinalizeEvent))]
    static void Postfix(MapEvent __instance)
    {
        // Call original if we called it
        if (CallPolicy.IsOriginalAllowed()) return;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

        var message = new MapEventDestroyed(__instance);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance, message);
    }
}
