using HarmonyLib;
using SandBox.View.Map.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch(typeof(MobilePartyVisualManager))]
internal class MobilePartyVisualManagerPatches
{
    [HarmonyPatch(nameof(MobilePartyVisualManager.Current), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool CurrentPrefix(ref MobilePartyVisualManager? __result)
    {
        __result = null;
        return false;
    }
}
