using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch]
internal class OnTickRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<OnTickRobustnessPatches>();

    [HarmonyPatch(typeof(Game), nameof(Game.OnTick))]
    [HarmonyFinalizer]
    private static Exception Finalizer_OnTick(Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "Failed to run {Method}", $"Game.OnTick");
        }

        return null;
    }
}
