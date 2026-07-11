using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

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
        static void Prefix(ref GameStateManager __instance, float dt)
        {
            if (!(__instance.ActiveState is MapState))
            {
                MapState mapState = __instance.LastOrDefault<MapState>();
                if (mapState == null) return;

                // Co-op keeps the (now backgrounded) map ticking so the world keeps simulating
                // without pausing while another screen (clan, kingdom, inventory, crafting, etc.) is on top.
                // Unlike vanilla, that forced tick also runs the inactive map handler's UI/input callbacks.
                // Those callbacks read stale input and can take focus from the active screen, which triggers
                // map hotkeys while typing and immediately clears fields such as the blacksmith weapon name.
                // Temporarily detach the handler so campaign simulation keeps ticking without the inactive
                // map UI. Always restore it in finally: if OnTick throws and the handler stays null, later map
                // input and lifecycle callbacks would remain broken for the rest of the session.
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
