using Common;
using GameInterface.Extentions;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Collections.Generic;
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

        // Don't tick food change for disconnected players
        if (playerManager.IsOwnerOfPartyDisconnected(party)) return false;

        // Use AI join window to determine if a player party should consume food and breed animals.
        // This way players only have food change at most once during a map event.
        if (party.MapEvent != null
            && !InteractionPatches.IsWithinAiJoinWindow(party.MapEvent)) return false;

        if (!ContainerProvider.TryResolve<IFoodConsumptionBehaviorInterface>(out var foodConsumptionBehaviorInterface)) return false;

        foodConsumptionBehaviorInterface.DailyTickParty(__instance, party);

        return false;
    }

    private static Dictionary<MobileParty, int> playerPartyLastItemVersions = new();

    [HarmonyPatch(nameof(FoodConsumptionBehavior.OnTick))]
    [HarmonyPrefix]
    public static bool OnTickPrefix(FoodConsumptionBehavior __instance, float dt)
    {
        foreach (var playerParty in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
        {
            int versionNo = playerParty.Party.ItemRoster.VersionNo;

            if (!playerPartyLastItemVersions.ContainsKey(playerParty))
            {
                playerPartyLastItemVersions[playerParty] = versionNo;
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
}