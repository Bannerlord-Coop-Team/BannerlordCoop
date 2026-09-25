using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(DefaultMarriageModel))]
internal class PlayerMarriageModelPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(DefaultMarriageModel.IsCoupleSuitableForMarriage))]
    public static bool IsCoupleSuitableForMarriagePrefix(DefaultMarriageModel __instance, Hero firstHero, Hero secondHero, ref bool __result)
    {
        if (!firstHero.IsPlayerHero() || !secondHero.IsPlayerHero()) return true;

        // Keep vanilla eligibility, allowing two player clan leaders to marry.
        if (!__instance.IsClanSuitableForMarriage(firstHero.Clan) ||
            !__instance.IsClanSuitableForMarriage(secondHero.Clan) ||
            firstHero.IsFemale == secondHero.IsFemale || __instance.AreHeroesRelated(firstHero, secondHero, 3))
        {
            __result = false;
            return false;
        }
        var firstCourtship = Romance.GetCourtedHeroInOtherClan(firstHero, secondHero);
        var secondCourtship = Romance.GetCourtedHeroInOtherClan(secondHero, firstHero);
        __result = (firstCourtship == null || firstCourtship == secondHero) &&
            (secondCourtship == null || secondCourtship == firstHero) &&
            firstHero.CanMarry() && secondHero.CanMarry();
        return false;
    }
}
