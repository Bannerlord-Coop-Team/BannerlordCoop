using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using GameInterface.Policies;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch(typeof(InitialState))]
internal class MainMenuEnteredPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnActivate")]
    static void OnActivate(ref InitialState __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        MessageBroker.Instance.Publish(__instance, new MainMenuEntered());
    }
}
