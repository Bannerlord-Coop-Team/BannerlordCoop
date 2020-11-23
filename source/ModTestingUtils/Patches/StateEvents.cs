using HarmonyLib;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BannerlordSystemTestingLibrary.Patches
{
    class StateEvents
    {
        public static event Action<string> OnStateActivate;
        public static event Action OnMainMenuReady;

        [HarmonyPatch(typeof(GameState), "OnActivate")]
        class PatchOnInitialize
        {
            static void Postfix(GameState __instance)
            {
                OnStateActivate?.Invoke(__instance.ToString());
            }
        }

        [HarmonyPatch(typeof(MBInitialScreenBase), "OnFrameTick")]
        class MainMenuReadyPatch
        {
            static void Postfix()
            {
                if (MBMusicManager.Current != null)
                {
                    OnMainMenuReady?.Invoke();
                }
            }
        }
    }
}
