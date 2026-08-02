using Common.Logging;
using GameInterface.Services.UI;
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
    private static void Postfix_Tick()
    {
        if (!IsLiveTestRun) return;

        LiveTestScreenThreadDispatcher.Update();
    }

    internal static bool HasLiveTestRunArgument(string[] arguments) =>
        Array.Exists(arguments, argument => string.Equals(argument, "/cooptestrun", StringComparison.OrdinalIgnoreCase));

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
