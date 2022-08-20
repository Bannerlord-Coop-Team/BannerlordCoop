using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Patch.GameStates
{
    [HarmonyPatch(typeof(GameManagerBase))]
    internal class GameLoadedPatch
    {
        
        class GameManagerPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch("OnLoadFinished")]
            static void OnGameLoaded(ref GameManagerBase __instance)
            {
                MessageBroker.Instance.Publish(__instance, new GameLoaded());
            }
        }
    }
}
