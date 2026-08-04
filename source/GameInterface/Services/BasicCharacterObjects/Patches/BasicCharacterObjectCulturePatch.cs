using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCharacterObjects.Patches;

[HarmonyPatch(nameof(BasicCharacterObject))]
internal class BasicCharacterObjectCulturePatch
{
    [HarmonyPatch(nameof(BasicCharacterObject.Culture), MethodType.Getter)]
    [HarmonyPostfix]
    public static void CultureGetterPostfix(BasicCharacterObject __instance, ref BasicCultureObject __result)
    {
        if (__result != null) return;

        if (__instance is not CharacterObject characterObject) return;

        var heroCulture = characterObject.HeroObject?.Culture;
        if (heroCulture == null) return;

        // Cache _culture
        __instance._culture = heroCulture;
        __result = heroCulture;
    }
}
