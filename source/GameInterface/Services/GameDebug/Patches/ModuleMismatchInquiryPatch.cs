using HarmonyLib;
using SandBox;
using System.Collections;

#if DEBUG
namespace GameInterface.Services.GameDebug.Patches
{
    /// <summary>
    /// Skip module mismatch popup when loading save (debug only)
    /// </summary>
    [HarmonyPatch(typeof(SandBoxSaveHelper))]
    internal class ModuleMismatchInquiryPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("CheckModules")]
        private static void CheckModulePostfix(ref IList __result)
        {
            // Result contains list of module changes, clear this to emulate compatibility.
            __result.Clear();
        }
    }
}
#endif