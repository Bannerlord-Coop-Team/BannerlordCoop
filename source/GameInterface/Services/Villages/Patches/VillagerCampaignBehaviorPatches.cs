using Common;
using Common.Messaging;
using GameInterface.Services.Villages.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillagerCampaignBehavior))]
internal class DisableVillagerCampaignBehaviorPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.HourlyTickSettlement)),
        AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.HourlyTickParty)),
        //AccessTools.Method(typeof(VillagerCampaignBehavior), nameof(VillagerCampaignBehavior.OnSessionLaunched)), // Needed on client to load dialogue for interacting with caravans
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
        var message = new DeleteExpiredLootedVillagers();
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}