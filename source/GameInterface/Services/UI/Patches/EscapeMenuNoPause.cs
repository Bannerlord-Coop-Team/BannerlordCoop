using Common;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Tournaments.UI;
using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.MountAndBlade.ViewModelCollection.EscapeMenu;

namespace GameInterface.Services.UI.Patches
{
    [HarmonyPatch(typeof(MapScreen))]
    internal class EscapeMenuNoPause
    {
        [HarmonyPatch("OnEscapeMenuToggled")]
        [HarmonyPrefix]
        static bool OnEscapeMenuToggled(MapScreen __instance, bool isOpened)
        {
            if (isOpened && !IsEscapeHotKeyReleased(__instance))
                return false;

            GameThread.Run(() =>
            {
                __instance.MapCameraView.OnEscapeMenuToggled(isOpened);
                if (__instance.IsEscapeMenuOpened == isOpened)
                {
                    return;
                }
                __instance.IsEscapeMenuOpened = isOpened;
                if (isOpened)
                {
                    List<EscapeMenuItemVM> escapeMenuItems = __instance.GetEscapeMenuItems();
                    //Game.Current.GameStateManager.RegisterActiveStateDisableRequest(this);
                    __instance._escapeMenuView = __instance.AddMapView<MapEscapeMenuView>(new object[]
                    {
                    escapeMenuItems
                    });
                    return;
                }
                __instance.RemoveMapView(__instance._escapeMenuView);
                __instance._escapeMenuView = null;
                //Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(this);

            }, blocking: true);

            return false;
        }

        private static bool IsEscapeHotKeyReleased(MapScreen mapScreen)
        {
            try
            {
                return mapScreen.SceneLayer?.Input?.IsHotKeyReleased("Exit") == true;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(MissionScreen))]
    internal class MissionEscapeMenuNoFocusLoss
    {
        [HarmonyPatch(nameof(MissionScreen.IsOpeningEscapeMenuOnFocusChangeAllowed))]
        [HarmonyPostfix]
        static void IsOpeningEscapeMenuOnFocusChangeAllowed(ref bool __result)
        {
            if (BattleSpawnGate.IsCoopBattleActive || IsCoopTournamentActive())
                __result = false;
        }

        internal static bool IsCoopTournamentActive() =>
            ContainerProvider.TryResolve<TournamentMissionUIContext>(out var context) &&
            context.TryGet(out _);
    }

    [HarmonyPatch(typeof(MBCommon), nameof(MBCommon.PauseGameEngine))]
    internal class TournamentEscapeMenuNoPause
    {
        [HarmonyPrefix]
        static bool PauseGameEngine() => !MissionEscapeMenuNoFocusLoss.IsCoopTournamentActive();
    }
}
