using HarmonyLib;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// <see cref="BasePath.Name"/> is a native getter for the game's root directory. Headless, we
    /// return the resolved game root (parent of bin\Win64_Shipping_Client) so module and save paths
    /// resolve without the engine.
    /// </summary>
    [HarmonyPatch(typeof(BasePath))]
    internal class BasePathPatches
    {
        /// <summary>Set by <see cref="HeadlessBootstrap"/> to the game root, with a trailing slash.</summary>
        public static string GameRootPath = "../../../../../mb2/";

        [HarmonyPatch(nameof(BasePath.Name), MethodType.Getter)]
        [HarmonyPrefix]
        public static bool NamePrefix(ref string __result)
        {
            __result = GameRootPath;
            return false;
        }
    }
}
