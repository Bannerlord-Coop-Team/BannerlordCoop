using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// <see cref="MBGameManager.IsEditModeOn"/> reads the native editor state (MBEditor.IsEditModeOn).
    /// Headless we are never in the editor.
    /// </summary>
    [HarmonyPatch(typeof(MBGameManager))]
    internal class GameManagerPatches
    {
        [HarmonyPatch(nameof(MBGameManager.IsEditModeOn), MethodType.Getter)]
        [HarmonyPrefix]
        static bool IsEditModeOnPrefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}
