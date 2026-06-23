using HarmonyLib;
using TaleWorlds.Library;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch(typeof(BasePath))]
internal class BasePathPatches
{
    [HarmonyPatch(nameof(BasePath.Name), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool NamePrefix(ref string __result)
    {
        __result = "../../../../../mb2/";
        return false;
    }
}
