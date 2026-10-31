using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Time.Patches
{
    [HarmonyPatch(typeof(GameStateManager))]
    class GameStateManagerPatches
    {
        // Prevents pausing in menus without their own game state (such as the encyclopedia)
        [HarmonyPatch(nameof(GameStateManager.RegisterActiveStateDisableRequest))]
        static bool Prefix() => false;

        // Prevents pausing in menus with their own game states (such as the banner editor, party screen, clan screen, etc.)
        [HarmonyPatch(nameof(GameStateManager.OnTick))]
        static bool Prefix(ref GameStateManager __instance, float dt)
        {
            if (__instance.ActiveState is MapState activeMapState)
            {
                // A screen pushed over the map without its own state (save/load, options) disables the map scene view,
                // and ticking map visuals against it crashes natively, so tick the campaign without the map handler
                if (activeMapState.Handler is not MapScreen mapScreen || ScreenManager.TopScreen == mapScreen) return true;

                __instance.CleanRequests();
                TickWithoutHandler(activeMapState, dt);
                return false;
            }

            MapState mapState = __instance.LastOrDefault<MapState>();
            if (mapState == null) return true;

            // Co-op keeps the (now backgrounded) map ticking so the world keeps simulating
            // without pausing while another screen (clan, kingdom, inventory, crafting, etc.) is on top.
            // Unlike vanilla, that forced tick also runs the inactive map handler's UI/input callbacks.
            // Those callbacks read stale input and can take focus from the active screen, which triggers
            // map hotkeys while typing and immediately clears fields such as the blacksmith weapon name.
            TickWithoutHandler(mapState, dt);
            return true;
        }

        // Always restore the handler: if OnTick throws and it stays null, map input and lifecycle callbacks stay broken
        private static void TickWithoutHandler(MapState mapState, float dt)
        {
            var handler = mapState.Handler;
            mapState.Handler = null;
            try
            {
                mapState.OnTick(dt);
            }
            finally
            {
                mapState.Handler = handler;
            }
        }
    }

    /// <summary>
    /// Prevents the inactive campaign map camera from replacing a mission's global audio listener.
    /// </summary>
    [HarmonyPatch(typeof(MapCameraView), nameof(MapCameraView.OnBeforeTick))]
    internal static class MapCameraViewPatches
    {
        [HarmonyPrefix]
        private static bool OnBeforeTickPrefix()
        {
            return ShouldTickMapCamera(GameStateManager.Current?.ActiveState);
        }

        internal static bool ShouldTickMapCamera(TaleWorlds.Core.GameState activeState)
        {
            // The map camera writes the global audio listener, which the active mission owns.
            return activeState is not MissionState;
        }
    }
}
