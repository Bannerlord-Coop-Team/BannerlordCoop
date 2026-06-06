using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.GuantletMapEventVisuals.Messages;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.GuantletMapEventVisuals.Patches;

[HarmonyPatch(typeof(GauntletMapEventVisual))]
internal class GauntletMapEventVisualPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<GauntletMapEventVisualPatches>();

    [HarmonyPatch(nameof(GauntletMapEventVisual.Initialize))]
    [HarmonyPrefix]
    private static void PrefixInitialize(GauntletMapEventVisual __instance, CampaignVec2 position, bool isVisible)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client called managed {Method}", nameof(GauntletMapEventVisual.Initialize));
            return;
        }

        var message = new GauntletMapEventVisualInitialized(__instance, position, isVisible);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
