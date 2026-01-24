using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;


/// <summary>
/// Used to sync number of enemies and allies spotted around.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class EntitiesSpottedSettlementPatch
{
    [HarmonyPatch(nameof(Settlement.NearbyLandThreatIntensity), MethodType.Setter)]
    [HarmonyPrefix]
    static bool NumberEnemiesSpottedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(Settlement.NearbyLandAllyIntensity), MethodType.Setter)]
    [HarmonyPrefix]
    static bool NumberAlliesSpottedPrefix() => ModInformation.IsServer;
}
