using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.Policies;

namespace GameInterface.Services.Settlements.Patches;


/// <summary>
/// Used to sync number of enemies and allies spotted around.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class EntitiesSpottedSettlementPatch
{
    [HarmonyPatch(nameof(Settlement.NearbyLandThreatIntensity), MethodType.Setter)]
    [HarmonyPrefix]
    static bool NumberEnemiesSpottedPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(Settlement.NearbyLandAllyIntensity), MethodType.Setter)]
    [HarmonyPrefix]
    static bool NumberAlliesSpottedPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
