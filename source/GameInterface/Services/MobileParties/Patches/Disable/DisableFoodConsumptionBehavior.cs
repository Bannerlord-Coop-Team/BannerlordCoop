using Common;
using GameInterface.Configuration;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(FoodConsumptionBehavior))]
internal class DisableFoodConsumptionBehavior
{
    [HarmonyPatch(nameof(FoodConsumptionBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(FoodConsumptionBehavior))]
internal class FoodConsumptionBehaviorPatches
{
    [HarmonyPatch(nameof(FoodConsumptionBehavior.OnPartyAttachedParty))]
    [HarmonyPrefix]
    public static bool OnPartyAttachedPartyPrefix(FoodConsumptionBehavior __instance, MobileParty mobileParty)
    {
        if (!ContainerProvider.TryResolve<IFoodConsumptionBehaviorInterface>(out var foodConsumptionBehaviorInterface)) return false;

        // Custom implementation to to work for all armies with player parties
        foodConsumptionBehaviorInterface.OnPartyAttachedParty(__instance, mobileParty);

        return false;
    }

    [HarmonyPatch(nameof(FoodConsumptionBehavior.DailyTickParty))]
    [HarmonyPrefix]
    public static bool DailyTickPartyPrefix(FoodConsumptionBehavior __instance, MobileParty party)
    {
        if (party == null || !party.IsPlayerParty()) return true;

        ContainerProvider.TryResolve<IPlayerManager>(out var playerManager);

        if (!ShouldTickFoodChange(playerManager, party)) return false;

        if (!ContainerProvider.TryResolve<IFoodConsumptionBehaviorInterface>(out var foodConsumptionBehaviorInterface)) return false;

        foodConsumptionBehaviorInterface.DailyTickParty(__instance, party);

        return false;
    }

    private static readonly ConditionalWeakTable<FoodConsumptionBehavior, Dictionary<MobileParty, int>> campaignPlayerPartyLastItemVersions = new();

    [HarmonyPatch(nameof(FoodConsumptionBehavior.OnTick))]
    [HarmonyPrefix]
    public static bool OnTickPrefix(FoodConsumptionBehavior __instance, float dt)
    {
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return false;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;

        var playerPartyLastItemVersions = campaignPlayerPartyLastItemVersions.GetOrCreateValue(__instance);

        foreach (var player in playerManager.Players)
        {
            var playerPartyId = player.MobilePartyId;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(playerPartyId, out var playerParty)) continue;

            if (!ShouldTickFoodChange(playerManager, playerParty)) continue;

            int versionNo = playerParty.Party.ItemRoster.VersionNo;

            if (!playerPartyLastItemVersions.ContainsKey(playerParty))
            {
                playerPartyLastItemVersions[playerParty] = -1;
            }

            if (playerParty.Party.IsStarving)
            {
                if (playerPartyLastItemVersions[playerParty] != versionNo)
                {
                    playerPartyLastItemVersions[playerParty] = versionNo;

                    __instance.PartyConsumeFood(playerParty, true);
                }
            }
        }

        return false;
    }

    [HarmonyPatch(nameof(FoodConsumptionBehavior.PartyConsumeFood))]
    [HarmonyPrefix]
    public static bool PartyConsumeFoodPrefix(FoodConsumptionBehavior __instance, MobileParty mobileParty, bool starvingCheck = false)
    {
        if (!ContainerProvider.TryResolve<IFoodConsumptionBehaviorInterface>(out var foodConsumptionBehaviorInterface)) return false;

        // Custom implementation to handle IsMainParty -> IsPlayerParty replacements and client notifications
        foodConsumptionBehaviorInterface.PartyConsumeFood(__instance, mobileParty, starvingCheck);

        return false;
    }

    [HarmonyPatch(nameof(FoodConsumptionBehavior.CheckAnimalBreeding))]
    [HarmonyPrefix]
    public static bool CheckAnimalBreedingPrefix(FoodConsumptionBehavior __instance, MobileParty party)
    {
        if (!ContainerProvider.TryResolve<IFoodConsumptionBehaviorInterface>(out var foodConsumptionBehaviorInterface)) return false;

        // Custom implementation to handle IsMainParty -> IsPlayerParty replacement and client notification
        foodConsumptionBehaviorInterface.CheckAnimalBreeding(__instance, party);

        return false;
    }

    private static bool ShouldTickFoodChange(IPlayerManager playerManager, MobileParty playerParty)
    {
        // Don't tick food change for disconnected players based on config
        if (ModInformation.IsServer
            && !ModConfigProvider.ModOptions.GoldFoodChangeForDisconnectedPlayers
            && playerManager.IsOwnerOfPartyDisconnected(playerParty)) return false;

        // Don't tick food change when party is in a settlement based on config
        if (playerParty.CurrentSettlement != null
            && !ModConfigProvider.ModOptions.GoldFoodChangeInSettlements) return false;

        // Party not in a map event, calculate gold change normally
        if (playerParty.MapEvent == null) return true;

        // Food change is disabled in battles, skip this tick
        if (ModConfigProvider.ModOptions.GoldFoodChangeInBattles == GoldFoodChangeMode.Disabled) return false;

        // Use gold food consumption window to determine if a player party should consume food based on config.
        // This way players only have food change at most once during a map event when set to OneDayMax.
        if (ModConfigProvider.ModOptions.GoldFoodChangeInBattles == GoldFoodChangeMode.OneDayMax
            && !InteractionPatches.IsWithinGoldFoodConsumptionWindow(playerParty.MapEvent)) return false;

        return true;
    }
}