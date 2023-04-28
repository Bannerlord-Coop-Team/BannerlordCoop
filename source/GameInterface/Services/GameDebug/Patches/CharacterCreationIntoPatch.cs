using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Interfaces;
using HarmonyLib;
using Serilog;
using TaleworldGameState = TaleWorlds.Core.GameState;

namespace GameInterface.Services.GameDebug.Patches
{
    [HarmonyPatch(typeof(TaleworldGameState))]
    internal class CharacterCreationIntoPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CharacterCreationIntoPatch>();

        [HarmonyPostfix]
        [HarmonyPatch("OnActivate")]
        private static void OnActivate(ref TaleworldGameState __instance)
        {
            Logger.Information("Game State is changing to {state}", __instance.GetType().Name);
            if (DebugCharacterCreationInterface.InCharacterCreationIntro())
            {
                MessageBroker.Instance.Publish(__instance, new CharacterCreationStarted());
            }
        }
    }
}
