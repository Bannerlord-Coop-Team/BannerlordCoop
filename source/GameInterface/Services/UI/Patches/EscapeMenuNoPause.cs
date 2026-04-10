using HarmonyLib;
using SandBox.View.Map;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.ViewModelCollection.EscapeMenu;

namespace GameInterface.Services.UI.Patches
{
    [HarmonyPatch(typeof(MapScreen))]
    internal class EscapeMenuNoPause
    {
        [HarmonyPatch("OnEscapeMenuToggled")]
        [HarmonyPrefix]
        static bool OnEscapeMenuToggled(ref MapScreen __instance, ref bool isOpened)
        {
            __instance.MapCameraView.OnEscapeMenuToggled(isOpened);
            if (__instance.IsEscapeMenuOpened == isOpened)
            {
                return false;
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
                return false;
            }
            __instance.RemoveMapView(__instance._escapeMenuView);
            __instance._escapeMenuView = null;
            //Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(this);

            return false;
        }
    }
}
