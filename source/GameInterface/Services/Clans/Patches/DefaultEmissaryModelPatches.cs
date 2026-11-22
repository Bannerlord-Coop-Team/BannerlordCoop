using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(DefaultEmissaryModel))]
internal class DefaultEmissaryModelPatches
{
    [HarmonyPatch(nameof(DefaultEmissaryModel.IsEmissary))]
    [HarmonyPrefix]
    public static bool IsEmissaryPrefix(ref bool __result, Hero hero)
    {
        __result = !hero.IsPlayerHero()
            && (hero.CompanionOf.IsPlayerClan() || hero.Clan.IsPlayerClan())
            && hero.PartyBelongedTo == null
            && hero.CurrentSettlement != null
            && hero.CurrentSettlement.IsFortification
            && !hero.IsPrisoner
            && hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge;
        return false;
    }
}
