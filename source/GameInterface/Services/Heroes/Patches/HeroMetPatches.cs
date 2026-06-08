using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Hero))]
internal class HeroMetPatches
{
    // Replace this later with a dictionary to properly manage each player knowing of every hero
    [HarmonyPatch(nameof(Hero.IsKnownToPlayer), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool IsKnownToPlayerGetterPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}
