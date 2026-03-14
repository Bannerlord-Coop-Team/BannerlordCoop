using HarmonyLib;
using SandBox;
using TaleWorlds.ModuleManager;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch(typeof(ModuleHelper))]
internal class ModuleHelperPatches
{
    [HarmonyPatch("_pathPrefix", MethodType.Getter)]
    [HarmonyPostfix]
    static void GetFaceIndexPrefix(ref string __result)
    {
        __result = "../../../../../mb2/Modules";
    }
}
