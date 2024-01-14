using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using SandBox.View.Map;
namespace GameInterface.Services.GameState.Patches
{
    [HarmonyPatch(typeof(MapScreen))]
    internal class GameExitedPatch 
    { 
    // Assume that the server is going to want to know that a player is exiting.
    // Thus this seems more like a prefix. May want to change depending on networking model. 

    [HarmonyPostfix]
    [HarmonyPatch("OnExit")]
    static void OnExit(ref MapScreen __instance)
    {
        MessageBroker.Instance.Publish(__instance, new GameExited());
    }

}
}
