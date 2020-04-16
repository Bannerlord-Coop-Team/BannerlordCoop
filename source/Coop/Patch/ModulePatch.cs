using Coop.Common;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Coop.Patch
{
    [HarmonyPatch(typeof(Module))]
    [HarmonyPatch(nameof(Module.SetInitialModuleScreenAsRootScreen))]
    class Patch_SetInitialModuleScreenAsRootScreen
    {
        static bool Prefix(Module __instance)
        {
            Log.Info("SetInitialModuleScreenAsRootScree!");
            return true;
        }
    }
}
