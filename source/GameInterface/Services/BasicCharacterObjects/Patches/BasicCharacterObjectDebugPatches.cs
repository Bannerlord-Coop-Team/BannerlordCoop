using HarmonyLib;
using GameInterface.Policies;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCharacterObjects.Patches;

[HarmonyPatch(typeof(BasicCharacterObject))]
internal class BasicCharacterObjectDebugPatches
{
    [HarmonyPatch(nameof(BasicCharacterObject.GetSkillValue))]
    static bool Prefix(BasicCharacterObject __instance, SkillObject skill, ref int __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (__instance?.DefaultCharacterSkills?.Skills != null)
        {
            __result = __instance.DefaultCharacterSkills.Skills.GetPropertyValue(skill);
        }
        else
        {
            __result = 0;
        }
        return false;
    }
}
