using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using GameInterface.Policies;

namespace GameInterface.Services.BasicCharacterObjects.Patches;

[HarmonyPatch(typeof(BasicCharacterObject))]
internal class BasicCharacterObjectDebugPatches
{
    [HarmonyPatch(nameof(BasicCharacterObject.GetSkillValue))]
    static bool Prefix(BasicCharacterObject __instance, SkillObject skill, ref int __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        __result = __instance.DefaultCharacterSkills.Skills.GetPropertyValue(skill);

        return false;
    }
}
