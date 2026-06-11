using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;

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
                // without pausing while another screen (clan, kingdom, inventory, etc.) is on top.
                // That tick still reads game-key input from the map's scene layer (navigation, time
                // control, ...) because the layer's "keys allowed" flag stays stale-true while the
                // map isn't the top screen (ScreenManager only refreshes top-screen / global layers).
                // While a text inquiry (e.g. the "Change Clan Name" box) is open we want those
                // keystrokes to go to the text box, so suppress the backgrounded map's keyboard input
                // for this tick and restore it afterwards. (IsKeysAllowed also gates the controller;
                // mouse input is already inert on a backgrounded map.)
                var mapInput = InformationManager.IsAnyInquiryActive() ? MapScreen.Instance?.SceneLayer?.Input : null;
                bool keysAllowed = mapInput?.IsKeysAllowed ?? false;
                if (mapInput != null) mapInput.IsKeysAllowed = false;

                mapState.OnTick(dt);

                if (mapInput != null) mapInput.IsKeysAllowed = keysAllowed;
            }
        }
    }
}
