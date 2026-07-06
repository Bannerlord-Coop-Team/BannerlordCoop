using HarmonyLib;
using System;
using TaleWorlds.Engine;

namespace Headless.Bootstrap.Patches
{
    /// <summary>
    /// <see cref="Utilities.GetFullCommandLineString"/> reads the process command line from the
    /// native engine, which is never initialised headless. The Coop mod uses it to parse the
    /// <c>/platformid &lt;id&gt;</c> argument into the client's controller id
    /// (<c>ControllerIdProvider.SetControllerFromProgramArgs</c>) — without this patch every
    /// headless client would fall back to the same default id and collide. Return the managed
    /// command line instead; the launcher may extend it (e.g. append a default
    /// <c>/platformid</c>) via <see cref="CommandLine"/>.
    /// </summary>
    [HarmonyPatch(typeof(Utilities), nameof(Utilities.GetFullCommandLineString))]
    internal class CommandLinePatches
    {
        /// <summary>The command line reported to the game. Defaults to the real one.</summary>
        public static string CommandLine = Environment.CommandLine;

        static bool Prefix(ref string __result)
        {
            __result = CommandLine;
            return false;
        }
    }
}
