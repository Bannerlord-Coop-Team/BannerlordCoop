using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.BasicCharacterObjects.Patches;

[HarmonyPatch(typeof(BasicCharacterObject))]
internal class BasicCharacterObjectPatches
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

    // Player characters can be missing base data from transfer after character creation, which FillFrom copies when creating offspring
    [HarmonyPatch(nameof(BasicCharacterObject.FillFrom))]
    [HarmonyPostfix]
    public static void FillFromPostfix(BasicCharacterObject __instance, BasicCharacterObject character)
    {
        if (__instance._culture == null)
        {
            // Use above getter to pick up the hero culture fallback
            var culture = character.Culture;
            if (culture != null)
            {
                __instance.Culture = culture;
            }
        }

        if (__instance.BodyPropertyRange == null)
        {
            // Vanilla player characters use the main_hero range
            var bodyPropertyRange = (character as CharacterObject)?.OriginalCharacter?.BodyPropertyRange
                ?? MBObjectManager.Instance.GetObject<CharacterObject>("main_hero")?.BodyPropertyRange;
            if (bodyPropertyRange != null)
            {
                // Repair template's properties for later reads during hero creation
                character.BodyPropertyRange ??= bodyPropertyRange;

                __instance.BodyPropertyRange = bodyPropertyRange;
            }
        }
    }
}
