using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.MobileParties.Patches.Disable;

/// <summary>
/// Disables party encounters
/// </summary>
[HarmonyPatch(typeof(EncounterManager))]
public class DisablePartyEncounterPatch
{
    [HarmonyPatch("StartPartyEncounter")]
    [HarmonyPrefix]
    private static bool StartPartyEncounterPrefix() => false;
}
