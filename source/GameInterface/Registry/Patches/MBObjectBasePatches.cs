using GameInterface.Policies;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.GameState;
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

        if (allowedTypes.Contains(__instance.GetType())) return true;

        return false;
    }

    public static HashSet<Type> allowedTypes = new HashSet<Type>()
    {
        typeof(MenuContext),
    };
}
