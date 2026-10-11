using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(ShipNameCampaignBehavior))]
internal class ShipNameCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(ShipNameCampaignBehavior.OnShipOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnShipOwnerChangedPrefix() => ModInformation.IsServer;
}
