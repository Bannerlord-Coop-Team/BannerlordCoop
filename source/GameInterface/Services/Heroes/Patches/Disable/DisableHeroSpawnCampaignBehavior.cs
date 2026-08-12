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
}
