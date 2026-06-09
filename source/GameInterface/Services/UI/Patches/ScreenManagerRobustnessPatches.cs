using Common.Logging;
using HarmonyLib;
using SandBox.ViewModelCollection.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch]
internal class ScreenManagerRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ScreenManagerRobustnessPatches>();
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(ScreenManager), nameof(ScreenManager.Tick))
    };

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
