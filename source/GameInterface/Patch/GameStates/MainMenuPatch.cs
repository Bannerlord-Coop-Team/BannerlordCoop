using GameInterface.Messages.Events;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Patch.GameStates
{
    internal class MainMenuPatch
    {
        private static bool MainMenuReady { get; set; } = false;

        [HarmonyPatch(typeof(MBInitialScreenBase), "OnFrameTick")]
        class MainMenuReadyPatch
        {
            static void Postfix(ref object __instance)
            {
                if (!MainMenuReady && MBMusicManager.Current != null)
                {
                    MainMenuReady = true;
                    GameInterface.MessageBroker?.Publish(__instance, new MainMenuEvent());
                }
            }
        }
    }
}
