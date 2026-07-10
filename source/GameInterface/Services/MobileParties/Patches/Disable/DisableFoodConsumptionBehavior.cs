using Common;
using GameInterface.Services.MobileParties.Interfaces;
using HarmonyLib;
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
        if (!ContainerProvider.TryResolve<IFoodConsumptionBehaviorInterface>(out var foodConsumptionBehaviorInterface)) return false;

        // Custom implementation to move check for starving player parties into daily tick from OnTick
        foodConsumptionBehaviorInterface.DailyTickParty(__instance, party);

        return false;
    }

    [HarmonyPatch(nameof(FoodConsumptionBehavior.OnTick))]
    [HarmonyPrefix]
    public static bool OnTickPrefix(FoodConsumptionBehavior __instance, float dt)
    {
        // Moved to be part of the daily tick.
        // Avoids tying the server hero's item roster version number to the behavior's version number 
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