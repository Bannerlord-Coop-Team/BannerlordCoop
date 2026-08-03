using Common;
using GameInterface.Configuration;
using GameInterface.Extentions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
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
        { 0, 0 }, // Tier 0: 0
        { 1, 0 }, // Tier 1: 1
        { 2, 5 }, // Tier 2: 2
        { 3, 5 }, // Tier 3: 3
        { 4, 5 }, // Tier 4: 4
        { 5, 10 }, // Tier 5: 5
        { 6, 10 }, // Tier 6: 6
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

        // Calculate scaling limit
        ContainerProvider.TryResolve<IPlayerManager>(out var playerManager);
        ContainerProvider.TryResolve<IObjectManager>(out var objectManager);

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
}