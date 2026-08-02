using Common;
using GameInterface.Policies;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches.Disable;

/// <summary>
/// NOT Disabled since this spawns parties and heroes at the beginning of the game
/// </summary>
[HarmonyPatch]
internal class DisableHeroSpawnCampaignBehavior
{
    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredMethods(typeof(HeroSpawnCampaignBehavior));

    [HarmonyPrefix]
    static bool Prefix() => ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed();
}

[HarmonyPatch(typeof(HeroSpawnCampaignBehavior))]
internal class HeroSpawnCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(HeroSpawnCampaignBehavior.TrySpawnHeroesAndParties))]
    [HarmonyPrefix]
    public static bool TrySpawnHeroesAndPartiesPrefix(Clan clan, bool isNewGame)
    {
        return clan == null || !clan.IsPlayerClan();
    }

    [HarmonyPatch(nameof(HeroSpawnCampaignBehavior.CanHeroMoveToAnotherSettlement))]
    [HarmonyPrefix]
    public static bool CanHeroMoveToAnotherSettlementPrefix(Hero hero)
    {
        return hero?.Clan == null || !hero.Clan.IsPlayerClan();
    }

    [HarmonyPatch(nameof(HeroSpawnCampaignBehavior.GetBestAvailableCommander))]
    [HarmonyPrefix]
    public static bool GetBestAvailableCommanderPrefix(HeroSpawnCampaignBehavior __instance, ref Hero __result, Clan clan)
    {
        if (clan == null || !clan.IsPlayerClan()) return true;

        // Replace check to not use Clan.PlayerClan check for player clans
        Hero hero = null;
        float num = 0f;
        foreach (Hero hero2 in clan.Heroes)
        {
            if (hero2.IsActive && hero2.IsAlive && hero2.PartyBelongedTo == null && hero2.PartyBelongedToAsPrisoner == null && hero2.CanLeadParty() && hero2.Age > (float)Campaign.Current.Models.AgeModel.HeroComesOfAge && hero2.CharacterObject.Occupation == Occupation.Lord)
            {
                float heroPartyCommandScore = __instance.GetHeroPartyCommandScore(hero2);
                if (heroPartyCommandScore > num)
                {
                    num = heroPartyCommandScore;
                    hero = hero2;
                }
            }
        }
        if (hero != null)
        {
            __result = hero;
            return false;
        }

        return false;
    }
}