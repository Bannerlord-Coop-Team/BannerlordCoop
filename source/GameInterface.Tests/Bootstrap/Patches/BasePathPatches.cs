using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
