using Common;
using Common.Messaging;
using GameInterface.Services.Villages.Handlers;
using GameInterface.Services.Villages.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillagerCampaignBehavior))]
internal class DisableVillagerCampaignBehaviorPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.HourlyTickSettlement)),
        //AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.HourlyTickParty)),
        //AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.OnSessionLaunched)), // Needed on client to load dialogue
        AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.OnSettlementEntered)),
        AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.DailyTick)),
        AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.OnMobilePartyDestroyed)),
        AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.OnLootDistributedToParty)),
        AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.OnSiegeEventStarted))
    };

    static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}

[HarmonyPatch(typeof(VillagerCampaignBehavior))]
internal class VillagerCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(VillagerCampaignBehavior.DeleteExpiredLootedVillagers))]
    [HarmonyPrefix]
    public static bool DeleteExpiredLootedVillagersPrefix(ref VillagerCampaignBehavior __instance)
    {
        if (ModInformation.IsClient)
            return false;
        
        var message = new DeleteExpiredLootedVillagers();
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    /// <summary>
    /// Separately block villager parties moving while in a conversation with a player.
    /// Villager parties don't use typical AI decision making and instead are given instructions directly from this method.
    /// </summary>
    [HarmonyPatch(nameof(VillagerCampaignBehavior.HourlyTickParty))]
    [HarmonyPrefix]
    public static bool HourlyTickPartyPrefix(ref VillagerCampaignBehavior __instance, MobileParty villagerParty)
    {
        if (ModInformation.IsClient)
            return false;
        
        // Block checks for parties that are invalid in the vanilla method
        if (!villagerParty.IsVillager || villagerParty.MapEvent != null || !villagerParty.HasLandNavigationCapability) return false;

        if (!ContainerProvider.TryResolve<VillagerCampaignBehaviorHandler>(out var villagerCampaignBehaviorHandler)) return true;

        return villagerCampaignBehaviorHandler.CanVillagerPartyMove(villagerParty);
    }
}
