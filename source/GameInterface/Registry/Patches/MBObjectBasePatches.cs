using Common.Util;
using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Registry.Patches;

[HarmonyPatch(typeof(MBObjectBase))]
internal class MBObjectBasePatches
{
    [HarmonyPatch(nameof(MBObjectBase.StringId), MethodType.Setter)]
    [HarmonyPrefix]
    private static bool StringIdPrefix(MBObjectBase __instance, string value)
    {
        // Call original if we allow this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (__instance.StringId == null)
        {
            using(new AllowedThread())
            {
                __instance.StringId = value;
            }
        }

        return false;
    }
}
