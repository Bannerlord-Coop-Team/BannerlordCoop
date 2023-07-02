using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Disables all map encounters/events
/// </summary>
[HarmonyPatch(typeof(EncounterManager))]
internal class EncounterManagerPatches
{
    [HarmonyPatch("StartPartyEncounter")]
    [HarmonyPrefix]
    private static bool StartPartyEncounterPrefix() => false;

    [HarmonyPatch("StartSettlementEncounter")]
    [HarmonyPrefix]
    private static bool StartSettlementEncounterPrefix() => false;
}
