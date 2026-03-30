using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Disables party encounters
/// </summary>
[HarmonyPatch(typeof(EncounterManager))]
public class DisablePartyEncounterPatch
{
    [HarmonyPatch("StartPartyEncounter")]
    [HarmonyPrefix]
    private static bool StartPartyEncounterPrefix()
    {
        InformationManager.DisplayMessage(new InformationMessage("Tried to start encounter"));
        return true;
    }
}
