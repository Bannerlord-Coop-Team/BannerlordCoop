using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using HarmonyLib;
using TaleworldGameState = TaleWorlds.Core.GameState;

namespace GameInterface.Services.GameDebug.Patches
{
    [HarmonyPatch(typeof(TaleworldGameState))]
    internal class CharacterCreationIntoPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnActivate")]
        private static void OnActivate(ref TaleworldGameState __instance)
        {
            if (DebugCharacterCreationInterface.InCharacterCreationIntro())
            {
                MessageBroker.Instance.Publish(__instance, new CharacterCreationStarted());
            }
        }
    }
}
