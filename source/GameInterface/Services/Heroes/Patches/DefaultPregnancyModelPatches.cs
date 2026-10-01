using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(DefaultPregnancyModel))]
internal class DefaultPregnancyModelPatches
{
    [HarmonyPatch(nameof(DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero))]
    [HarmonyPrefix]
    public static bool GetDailyChanceOfPregnancyForHeroPrefix(DefaultPregnancyModel __instance, Hero hero, ref float __result)
    {
        int newNoChildren = hero.Children.Count + 1;
        float targetHeroCount = (4 + 4 * hero.Clan.Tier);
        int actualCount = hero.Clan.AliveLords.Count;

        float scalingFactor = (!hero.IsPlayerHero() && !hero.Spouse.IsPlayerHero()) 
            ? Math.Min(1f, (2f * targetHeroCount - (float)actualCount) / targetHeroCount) : 1f;

        float chance = (1.2f - (hero.Age - 18f) * 0.04f) / (float)(newNoChildren * newNoChildren) * 0.12f * scalingFactor;
        float baseNumber = (hero.Spouse != null && __instance.IsHeroAgeSuitableForPregnancy(hero)) ? chance : 0f;
        ExplainedNumber explainedNumber = new(baseNumber, false, null);

        if (hero.GetPerkValue(DefaultPerks.Charm.Virile) || hero.Spouse.GetPerkValue(DefaultPerks.Charm.Virile))
        {
            explainedNumber.AddFactor(DefaultPerks.Charm.Virile.PrimaryBonus, DefaultPerks.Charm.Virile.Name);
        }

        __result = explainedNumber.ResultNumber;
        return false;
    }
}
