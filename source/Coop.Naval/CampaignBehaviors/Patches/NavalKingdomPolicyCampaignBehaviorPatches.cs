using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalKingdomPolicyCampaignBehaviour))]
internal class NavalKingdomPolicyCampaignBehaviourPatches
{
    [HarmonyPatch(nameof(NavalKingdomPolicyCampaignBehaviour.OnBarterAccepted))]
    [HarmonyPrefix]
    public static bool OnBarterAcceptedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalKingdomPolicyCampaignBehaviour.OnDailyTick))]
    [HarmonyPrefix]
    public static bool OnDailyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalKingdomPolicyCampaignBehaviour.OnHourlyTickParty))]
    [HarmonyPrefix]
    public static bool OnHourlyTickPartyPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalKingdomPolicyCampaignBehaviour.OnMobilePartyDestroyed))]
    [HarmonyPrefix]
    public static bool OnMobilePartyDestroyedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalKingdomPolicyCampaignBehaviour.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnSettlementOwnerChangedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalKingdomPolicyCampaignBehaviour.OnRaidCompleted))]
    [HarmonyPrefix]
    public static bool OnRaidCompletedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalKingdomPolicyCampaignBehaviour.OnShipOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnShipOwnerChangedPrefix() => ModInformation.IsServer;
}
