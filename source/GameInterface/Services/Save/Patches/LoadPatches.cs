using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Save.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.Save.Patches
{
    [HarmonyPatch(typeof(SaveManager), "Load", new Type[] { typeof(string), typeof(ISaveDriver), typeof(bool) })]
    internal class LoadPatches
    {
        static void Postfix(Game __instance, ref LoadResult __result, ref string saveName)
        {
            if (__result.Successful)
            {
                MessageBroker.Instance.Publish(__instance, new GameLoaded(saveName));
            }
        }
    }
}
