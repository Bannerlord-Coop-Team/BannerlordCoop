using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCharacterObjects.Patches;

[HarmonyPatch(typeof(BasicCharacterObject))]
internal class BasicCharacterObjectDebugPatches
{
    [HarmonyPatch(nameof(BasicCharacterObject.GetSkillValue))]
    static bool Prefix(BasicCharacterObject __instance, SkillObject skill, ref int __result)
    {
        __instance.DefaultCharacterSkills.Skills.GetPropertyValue(skill);

        return false;
    }
}
