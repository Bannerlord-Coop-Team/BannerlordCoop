using System;
using Coop.Mod.CLI;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Coop.Mod.Patch
{
    public static class Debugging
    {
        public static IDebugManager DebugManager { get; } = new DebugManager();

        [HarmonyPatch(typeof(MBDebug))]
        [HarmonyPatch(nameof(MBDebug.Print))]
        class PatchPrint
        {
            static bool Prefix(ref string message, int logLevel, Debug.DebugColor color, ulong debugFilter)
            {
                DebugManager.Print(message, logLevel, color, debugFilter);
                return false;
            }
        }
    }
}
