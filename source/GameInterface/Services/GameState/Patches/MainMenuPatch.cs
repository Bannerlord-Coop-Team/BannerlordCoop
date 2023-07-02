using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.GameState.Patches;

internal class MainMenuPatch
{
    [HarmonyPatch(typeof(InitialState))]
    class MainMenuEnteredPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnActivate")]
        static void OnActivate(ref InitialState __instance)
        {
            MessageBroker.Instance.Publish(__instance, new MainMenuEntered());
        }
    }
}
