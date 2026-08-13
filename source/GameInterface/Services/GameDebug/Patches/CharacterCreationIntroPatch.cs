using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleworldGameState = TaleWorlds.Core.GameState;
using TaleworldCharacterCreationState = TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState;

namespace GameInterface.Services.GameDebug.Patches;

// Only skip for debugging
[HarmonyPatch(typeof(TaleworldGameState))]
internal class CharacterCreationIntroPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CharacterCreationIntroPatch>();

    [HarmonyPostfix]
    [HarmonyPatch("OnActivate")]
    private static void OnActivate(ref TaleworldGameState __instance)
    {
        Logger.Information("Game State is changing to {state}", __instance.GetType().Name);

        // GameLoadingState and MapState also activate while an existing player is validating or
        // loading the transferred save. Publishing for either one moves the client out of
        // ValidateModuleState before the server's existing-player response can arrive.
        if (IsCharacterCreationState(__instance.GetType()))
        {
            MessageBroker.Instance.Publish(__instance, new CharacterCreationStarted());
        }

#if DEBUG
        if (DebugCharacterCreationInterface.InCharacterCreationIntro())
        {
            // The DEBUG fast path previously depended on the lifecycle event above. Keep the
            // automation behavior without misreporting unrelated game-state transitions as
            // character creation.
            if (ContainerProvider.TryResolve<IDebugCharacterCreationInterface>(out var characterCreationInterface))
            {
                characterCreationInterface.SkipCharacterCreation();
            }

            if (VideoPlayerViewPatch.CurrentVideoPlayerView != null)
            {
                VideoPlayerViewPatch.CurrentVideoPlayerView?.StopVideo();
                VideoPlayerViewPatch.CurrentVideoPlayerView = null;
            }
        }
#endif
    }

    internal static bool IsCharacterCreationState(Type stateType) =>
        typeof(TaleworldCharacterCreationState).IsAssignableFrom(stateType);
}
