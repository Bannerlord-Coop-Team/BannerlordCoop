using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.Engine;

namespace GameInterface.Services.UI.Patches;

///<summary>
/// Prevents the TaleWorlds debug console clear command from crashing game sessions that do not
/// have an attached Windows system console by replacing the engine implementation with a safe
/// Harmony prefix.
/// </summary>
[HarmonyPatch(typeof(MBDebug))]
internal static class MBDebugPatch
{
    [HarmonyPatch(nameof(MBDebug.ClearConsole))]
    [HarmonyPrefix]
    private static bool ClearConsolePrefix(List<string> strings, ref string __result)
    {
        // Bannerlord may run without an attached Windows console, which makes Console.Clear unsafe.
        try
        {
            Console.Clear();
            __result = "Debug console cleared.";
        }
        catch (IOException)
        {
            __result = "Clearing the system console is not supported in this environment.";
        }
        catch (InvalidOperationException)
        {
            __result = "Clearing the system console is not supported in this environment.";
        }

        return false;
    }
}