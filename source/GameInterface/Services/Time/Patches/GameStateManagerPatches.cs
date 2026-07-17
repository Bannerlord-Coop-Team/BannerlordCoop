using Common;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
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
            var activeState = __instance.ActiveState;
            if (activeState is MapState) return;

            bool isCoopFieldBattleActive =
                Campaign.Current?.MainParty?.MapEvent?.IsFieldBattle == true &&
                BattleSpawnGate.IsCoopBattleActive;
            if (!ShouldRunBackgroundCampaignSimulation(
                    activeState,
                    ModInformation.IsClient,
                    isCoopFieldBattleActive))
            {
                return;
            }

            MapState mapState = __instance.LastOrDefault<MapState>();
            if (mapState == null) return;

            // Co-op keeps the background map simulating under non-battle screens without ticking its UI.
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

        internal static bool ShouldRunBackgroundCampaignSimulation(
            TaleWorlds.Core.GameState activeState,
            bool isClient,
            bool isCoopFieldBattleActive)
        {
            return !isClient || activeState is not MissionState || !isCoopFieldBattleActive;
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
