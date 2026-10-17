using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(DefaultInformationRestrictionModel))]
internal class InformationRestrictionModelPatches
{
    // The IsKnownToPlayer getter prefix is skipped when this caller was jitted with the getter inlined before patching
    [HarmonyPatch(nameof(DefaultInformationRestrictionModel.DoesPlayerKnowDetailsOf), typeof(Hero))]
    [HarmonyPrefix]
    public static bool DoesPlayerKnowDetailsOfHeroPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}
