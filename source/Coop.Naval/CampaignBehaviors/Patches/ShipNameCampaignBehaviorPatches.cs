using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(ShipNameCampaignBehavior))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class ShipNameCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(ShipNameCampaignBehavior.OnShipOwnerChanged))]
    [HarmonyPrefix]
    public static bool OnShipOwnerChangedPrefix() => ModInformation.IsServer;
}
