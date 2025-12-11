using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;

namespace GameInterface.Services.Time.Patches
{
    [HarmonyPatch(typeof(GameStateManager))]
    class GameStateManagerPatches
    {
        private static readonly MethodInfo MapState_OnTick = typeof(MapState).GetMethod("OnTick", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(nameof(GameStateManager.OnTick))]
        static void OnTick_Postfix(ref GameStateManager __instance, float dt)
        {
            if (__instance.ActiveState is MapState) return;

            var mapState = __instance.LastOrDefault<MapState>();
            if (mapState == null) return;

            if (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement != null)
            {
                MapState_OnTick?.Invoke(mapState, new object[] { dt });
            }
        }

    }
}
