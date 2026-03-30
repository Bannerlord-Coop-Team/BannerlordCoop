using Common;
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
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(MapEvent));
            return false;
        }

        return true;
    }
}
