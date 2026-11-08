using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

[HarmonyPatch]
internal class PlayerCaptivityRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerCaptivityRobustnessPatches>();

    // Nobody plays the server's main hero, so if it is captured its captivity menu can never be clicked, and an
    // open menu zeroes the campaign dt so no party AI thinks. Players' captivity runs in PlayerCaptivityServerHandler.
    [HarmonyPatch(typeof(PlayerCaptivity), nameof(PlayerCaptivity.Update))]
    [HarmonyPrefix]
    private static bool Prefix_Update() => !ModInformation.IsServer;

    [HarmonyPatch(typeof(PlayerCaptivity), nameof(PlayerCaptivity.Update))]
    [HarmonyFinalizer]
    private static Exception Finalizer_UpdateMapEventsAux(Exception __exception, MethodBase __originalMethod)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "Failed to run {Method}", $"{__originalMethod.DeclaringType}.{__originalMethod.Name}");
        }

        return null;
    }
}
