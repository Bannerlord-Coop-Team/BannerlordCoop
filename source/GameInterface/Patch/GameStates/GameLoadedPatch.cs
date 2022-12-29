using HarmonyLib;
using SandBox;
using TaleWorlds.Core;

namespace GameInterface.Patch.GameStates
{
    [HarmonyPatch(typeof(SandBoxGameManager))]
    internal class GameLoadedPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnLoadFinished")]
        static void OnGameLoaded(ref GameManagerBase __instance)
        {
            //MessageBroker.Instance.Publish(__instance, new GameLoaded());
        }
    }
}
