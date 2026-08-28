using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Services.UI.Patches;
using HarmonyLib;
using Serilog;
using System;
using System.IO;
using TaleworldGameState = TaleWorlds.Core.GameState;
using TaleworldCharacterCreationState = TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState;
using TaleworldVideoPlaybackState = TaleWorlds.MountAndBlade.VideoPlaybackState;

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

        var isCharacterCreationIntro = IsCharacterCreationIntro(__instance);
        if (ShouldHideCoopLoadingWindow(
                isCharacterCreationIntro,
                LoadingWindowPatches.ForceLoadingWindow,
                Common.ModInformation.IsClient) &&
            ContainerProvider.TryResolve<ILoadingInterface>(out var loadingInterface))
        {
            loadingInterface.HideLoadingScreen();
        }

        // GameLoadingState and MapState also activate while an existing player is validating or
        // loading the transferred save. Publishing for either one moves the client out of
        // ValidateModuleState before the server's existing-player response can arrive.
        if (IsCharacterCreationState(__instance.GetType()))
        {
            MessageBroker.Instance.Publish(__instance, new CharacterCreationStarted());
        }

#if DEBUG
        if (isCharacterCreationIntro)
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

    internal static bool IsCharacterCreationIntro(TaleworldGameState state)
    {
        if (state is not TaleworldVideoPlaybackState videoState ||
            string.IsNullOrEmpty(videoState.VideoPath))
        {
            return false;
        }

        var normalizedPath = videoState.VideoPath.Replace('\\', '/');
        var videoName = Path.GetFileNameWithoutExtension(normalizedPath);
        return string.Equals(videoName, "campaign_intro", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ShouldHideCoopLoadingWindow(
        bool isCharacterCreationIntro,
        bool forceLoadingWindow,
        bool isClient) =>
        isClient && forceLoadingWindow && isCharacterCreationIntro;
}
