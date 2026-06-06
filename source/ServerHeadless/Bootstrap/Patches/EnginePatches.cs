using HarmonyLib;
using TaleWorlds.Engine;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Native engine calls that are reached through the Coop load path but have no meaning headless.
    /// </summary>
    [HarmonyPatch]
    internal class EnginePatches
    {
        // DebugGameInterface.StartGame calls MouseManager.ShowCursor (native) after loading.
        [HarmonyPatch(typeof(MouseManager), nameof(MouseManager.ShowCursor))]
        [HarmonyPrefix]
        static bool ShowCursorPrefix() => false;
    }
}
