using Common;
using HarmonyLib;
using SandBox.View;
using SandBox.View.Map;

namespace GameInterface.Services.PartyVisuals.Patches;

/// <summary>Suppresses server map rendering while preserving vanilla scene maintenance.</summary>
[HarmonyPatch]
internal static class ServerMapVisualTickPatches
{
    [HarmonyPatch(typeof(SandBoxViewVisualManager), nameof(SandBoxViewVisualManager.OnTick))]
    [HarmonyPrefix]
    private static bool SandBoxViewVisualManagerOnTickPrefix() => ModInformation.IsClient;

    [HarmonyPatch(typeof(SandBoxViewVisualManager), nameof(SandBoxViewVisualManager.OnFrameTick))]
    [HarmonyPrefix]
    private static bool SandBoxViewVisualManagerOnFrameTickPrefix() => ModInformation.IsClient;

    [HarmonyPatch(typeof(MapScreen), nameof(MapScreen.TickVisuals))]
    [HarmonyPrefix]
    private static void MapScreenTickVisualsPrefix(out bool __state)
    {
        __state = MapScreen.DisableVisualTicks;
        if (ModInformation.IsServer)
        {
            MapScreen.DisableVisualTicks = true;
        }
    }

    [HarmonyPatch(typeof(MapScreen), nameof(MapScreen.TickVisuals))]
    [HarmonyFinalizer]
    private static void MapScreenTickVisualsFinalizer(bool __state)
    {
        if (ModInformation.IsServer)
        {
            MapScreen.DisableVisualTicks = __state;
        }
    }
}
