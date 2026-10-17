using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Don't eject player from settlement early.
/// Player encounter battle finalisation from defending in sieges is skipped otherwise.
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish))]
internal static class SiegeDefenderFinishPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref bool forcePlayerOutFromSettlement)
    {
        if (ModInformation.IsServer) return;

        var encounter = PlayerEncounter.Current;
        if (encounter?._mapEvent?.IsSiegeAssault == true &&
            encounter.EncounterState == PlayerEncounterState.End &&
            encounter.PlayerSide == BattleSideEnum.Defender)
        {
            forcePlayerOutFromSettlement = false;
        }
    }
}
