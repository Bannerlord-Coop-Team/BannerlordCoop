using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(ShipProductionCampaignBehavior))]
internal class ShipProductionCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(ShipProductionCampaignBehavior.DailyTickTown))]
    [HarmonyPrefix]
    public static bool DailyTickTownPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(ShipProductionCampaignBehavior.OnShipCreated))]
    [HarmonyPrefix]
    public static bool OnShipCreatedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(ShipProductionCampaignBehavior.OnShipDestroyed))]
    [HarmonyPrefix]
    public static bool OnShipDestroyedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(ShipProductionCampaignBehavior.OnShipOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnShipOwnerChangedPrefix() => ModInformation.IsServer;
}
