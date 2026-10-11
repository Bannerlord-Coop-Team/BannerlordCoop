using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalPatrolPartiesCampaignBehavior))]
internal class NavalPatrolPartiesCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.DailyTickSettlement))]
    [HarmonyPrefix]
    public static bool DailyTickSettlementPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.RemoveSettlementParties))]
    [HarmonyPrefix]
    public static bool RemoveSettlementPartiesPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.SettlementEntered))]
    [HarmonyPrefix]
    public static bool SettlementEnteredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.AiHourlyTick))]
    [HarmonyPrefix]
    public static bool AiHourlyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.OnSettlementLeft))]
    [HarmonyPrefix]
    public static bool OnSettlementLeftPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.OnMobilePartyDestroyed))]
    [HarmonyPrefix]
    public static bool OnMobilePartyDestroyedPrefix() => ModInformation.IsServer;
}
