using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch]
internal class ScreenManagerRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ScreenManagerRobustnessPatches>();
#if DEBUG
    private static readonly bool IsLiveTestRun = HasLiveTestRunArgument(Environment.GetCommandLineArgs());
#endif

    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(ScreenManager), nameof(ScreenManager.Tick))
    };

#if DEBUG
    [HarmonyPostfix]
    private static void Postfix_Tick(float dt)
    {
        GameThread gameThread = GameThread.Instance;
        if (!ShouldPumpGameThread(IsLiveTestRun, gameThread.IsGameThread, gameThread.QueueLength)) return;

        gameThread.Update(TimeSpan.FromSeconds(dt));
    }

    internal static bool HasLiveTestRunArgument(string[] arguments) =>
        Array.Exists(arguments, argument => string.Equals(argument, "/cooptestrun", StringComparison.OrdinalIgnoreCase));

    internal static bool ShouldPumpGameThread(bool isLiveTestRun, bool isGameThread, int queueLength) =>
        isLiveTestRun && isGameThread && queueLength > 0;
#endif

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
