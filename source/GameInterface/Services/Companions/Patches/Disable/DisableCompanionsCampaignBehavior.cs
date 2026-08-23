using Common;
using GameInterface.Configuration;
using GameInterface.Extentions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;

namespace GameInterface.Services.Companions.Patches.Disable;

[HarmonyPatch(typeof(CompanionsCampaignBehavior))]
internal class DisableCompanionsCampaignBehavior
{
    [HarmonyPatch(nameof(CompanionsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(CompanionsCampaignBehavior))]
internal class CompanionsCampaignBehaviorPatches
{
    // Alternative way to increase companion limit scaled by player clan tiers suggested by tester.
    // See "wandererLimitScalesWithPlayers" in mod-config.default.json for a more detailed description of what this config is.
    private static readonly Dictionary<int, int> ClanTierScalingValues = new()
    {
        { 0, 0 },
        { 1, 0 },
        { 2, 5 },
        { 3, 5 },
        { 4, 5 },
        { 5, 10 },
        { 6, 10 },
    };

    [HarmonyPatch(nameof(CompanionsCampaignBehavior._desiredTotalCompanionCount), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool DesiredTotalCompanionCountGetterPrefix(ref float __result)
    {
        // Use fixed wanderer limit
        if (!ModConfigProvider.ModOptions.WandererLimitScalesWithPlayers)
        {
            __result = ModConfigProvider.ModOptions.WandererLimit;
            return false;
        }

        // Calculate scaling limit. Session teardown clears the container without unpatching, so this
        // prefix can outlive it — fall through to vanilla rather than NRE on the resolved managers
        // (vanilla's count formula doesn't touch Hero.MainHero, so it is safe on a headless server).
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return true;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return true;

        // Start with vanilla limit
        var total = 32;
        foreach (var player in playerManager.Players)
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(player.ClanId, out var playerClan))
                continue;

            if (ClanTierScalingValues.TryGetValue(playerClan.Tier, out int valueFromTier))
                total += valueFromTier;
        }

        __result = total;
        return false;
    }

    /// <summary>
    /// Replace vanilla implementation to not use Hero.MainHero which is null on the headless server.
    /// </summary>
    [HarmonyPatch(nameof(CompanionsCampaignBehavior.TrySpawnNewCompanion))]
    [HarmonyPrefix]
    public static bool TrySpawnNewCompanionPrefix(CompanionsCampaignBehavior __instance)
    {
        if ((float)__instance._aliveCompanionTemplates.Count < __instance._desiredTotalCompanionCount)
        {
            Town targetTown = Town.AllTowns.GetRandomElementWithPredicate(delegate (Town x)
            {
                // Instead of checking if Hero.MainHero is in the settlement, check if any players are in the settlement
                bool playerInSettlement = false;
                foreach(var playerHero in Campaign.Current.CampaignObjectManager.GetPlayerHeroes())
                {
                    if (playerHero.CurrentSettlement == x.Settlement)
                    {
                        playerInSettlement = true;
                        break;
                    }
                }

                if (!playerInSettlement && x.Settlement.SiegeEvent == null)
                {
                    return x.Settlement.HeroesWithoutParty.AllQ(y => !y.IsWanderer || y.CompanionOf != null);
                }
                return false;
            });

            Settlement targetSettlement = targetTown?.Settlement;
            if (targetSettlement != null)
            {
                __instance.CreateCompanionAndAddToSettlement(targetSettlement);
            }
        }

        return false;
    }

    [HarmonyPatch(nameof(CompanionsCampaignBehavior.GetCompanionTemplateToSpawn))]
    [HarmonyPostfix]
    public static void GetCompanionTemplateToSpawnPostfix(CompanionsCampaignBehavior __instance, ref CharacterObject __result)
    {
        // Vanilla limit hit for this template, use a random from the pool
        if (__result == null)
        {
            List<CharacterObject> list = __instance._companionsOfTemplates[__instance.GetCompanionTemplateTypeToSpawn()];
            list.Shuffle<CharacterObject>();

            var random = new Random();
            var characterObject = list[random.Next(list.Count)];

            __result = characterObject;
        }
    }

    [HarmonyPatch(nameof(CompanionsCampaignBehavior.GetCompanionTemplateTypeToSpawn))]
    [HarmonyPostfix]
    public static void GetCompanionTemplateTypeToSpawnPostfix(CompanionsCampaignBehavior __instance, ref CompanionsCampaignBehavior.CompanionTemplateType __result)
    {
        // Vanilla limit hit for for all templates, use a random type
        // Combat represents vanilla's default and doesn't have any templates
        if (__result == CompanionsCampaignBehavior.CompanionTemplateType.Combat)
        {
            var random = new Random();

            var values = Enum.GetValues(typeof(CompanionsCampaignBehavior.CompanionTemplateType));
            var randomTemplateType = (CompanionsCampaignBehavior.CompanionTemplateType)values.GetValue(random.Next(values.Length));

            __result = randomTemplateType;
        }
    }

    /// <summary>
    /// Add a check to allow stuck heroes to become fugitive if they didn't properly get released.
    /// This is primarily for older saves where some players would have companions stuck as prisoners.
    /// </summary>
    [HarmonyPatch(nameof(CompanionsCampaignBehavior.DailyTick))]
    [HarmonyPostfix]
    public static void DailyTickPostfix()
    {
        RepairStuckHeroes(Hero.AllAliveHeroes, hero => MakeHeroFugitiveAction.Apply(hero, true));
    }

    internal static void RepairStuckHeroes(IEnumerable<Hero> heroes, Action<Hero> makeHeroFugitive)
    {
        foreach (Hero hero in heroes)
        {
            if (hero.IsPrisoner && hero.PartyBelongedToAsPrisoner == null)
            {
                makeHeroFugitive(hero);
            }
        }
    }
}