using GameInterface.Policies;
using GameInterface.Utils;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.HeroDevelopers.Patches
{
    [HarmonyPatch(typeof(HeroDeveloper))]
    internal class SetFocusPatch
    {
        private static readonly FieldInfo HeroDeveloperFocusesField = AccessTools.Field(typeof(HeroDeveloper), nameof(HeroDeveloper._newFocuses));

        [HarmonyPatch(nameof(HeroDeveloper.SetFocus))]
        [HarmonyPrefix]
        private static bool SetFocusPrefix(HeroDeveloper __instance, SkillObject focus, int newAmount)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            var newFocuses = __instance._newFocuses;
            if (newFocuses == null) return true;

            if (!GenericPatchHelpers.PropertyOwnerSetInterceptCache.TryGetValue(HeroDeveloperFocusesField, out var setIntercept))
                return true;

            if ((newFocuses.GetPropertyValue(focus)) == newAmount) return false;

            setIntercept.Invoke(null, new object[] { __instance, newFocuses, focus, newAmount});

            return false;
        }
    }
}
