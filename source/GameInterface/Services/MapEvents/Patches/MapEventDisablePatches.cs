using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
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
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
            return false;
        }

        return true;
    }
}
