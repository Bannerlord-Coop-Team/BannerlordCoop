using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(DefaultRansomValueCalculationModel))]
internal class RansomPlayerValuePatch
{
    [HarmonyPatch(nameof(DefaultRansomValueCalculationModel.PrisonerRansomValue))]
    [HarmonyPrefix]
    public static bool PrisonerRansomValuePrefix(ref int __result, CharacterObject prisoner, Hero sellerHero = null)
    {
        if (prisoner.IsHero && prisoner.HeroObject.IsPlayerHero())
        {
            __result = 0;
            return false;
        }

        return true;
    }
}
