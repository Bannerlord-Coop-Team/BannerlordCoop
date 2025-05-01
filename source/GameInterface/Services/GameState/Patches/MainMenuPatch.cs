using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch(typeof(InitialState))]
internal class MainMenuEnteredPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnActivate")]
    static void OnActivate(ref InitialState __instance)
    {
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(__instance, new MainMenuEntered());
    }
}
