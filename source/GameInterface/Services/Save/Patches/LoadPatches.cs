using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Services.Heroes.Patches;

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
