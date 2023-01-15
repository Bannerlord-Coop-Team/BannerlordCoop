using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using SandBox;
using TaleWorlds.Core;

namespace GameInterface.Services.GameState.Patches
{
    [HarmonyPatch(typeof(SandBoxGameManager))]
    internal class GameLoadedPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnLoadFinished")]
        static void OnGameLoaded(ref GameManagerBase __instance)
        {
            MessageBroker.Instance.Publish(__instance, new GameLoaded());
        }
    }
}
