using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
internal class MapEventDisablePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventCollectionPatches>();

    [HarmonyPatch(nameof(MapEvent.Initialize))]
    [HarmonyPrefix]
    static bool InitializePrefix()
    {
        // Call original if we called it
        if (CallPolicy.IsOriginalAllowed()) return true;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

        return true;
    }
}
