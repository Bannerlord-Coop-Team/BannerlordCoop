using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

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

    // OnTick clears tickCompleted before the behavior ticks and only the agent tick at its end sets it again,
    // so a throw in between would leave the next OnPreTick spinning in WaitTickCompletion forever.
    // The async agent tick is OnTick's last call, so a caught throw means it was never started.
    [HarmonyPatch(typeof(Mission), nameof(Mission.OnTick))]
    [HarmonyFinalizer]
    internal static void Finalizer_MissionOnTick(Mission __instance, Exception __exception)
    {
        if (__exception == null) return;

        __instance.tickCompleted = true;
    }
}
