﻿using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using HarmonyLib;
using Serilog;
using TaleworldGameState = TaleWorlds.Core.GameState;

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
        
        MessageBroker.Instance.Publish(__instance, new CharacterCreationStarted());

#if DEBUG
        if (DebugCharacterCreationInterface.InCharacterCreationIntro())
        {
            if (VideoPlayerViewPatch.CurrentVideoPlayerView != null)
            {
                VideoPlayerViewPatch.CurrentVideoPlayerView?.StopVideo();
                VideoPlayerViewPatch.CurrentVideoPlayerView = null;
            }
        }
#endif
    }
}
