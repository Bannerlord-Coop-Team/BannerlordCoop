using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Keeps a winning inside siege defender in its town when the after battle flow finishes its encounter.
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish))]
internal static class SiegeDefenderFinishPatch
{
    // Game-thread only. The town of a defender victory the server named this client for, set by
    // SiegeDefenderVictoryHandler until it seats the defender on the siege-defeated menu.
    internal static Settlement PendingDefenderVictorySettlement;

    [HarmonyPrefix]
    private static void Prefix(ref bool forcePlayerOutFromSettlement)
    {
        if (ModInformation.IsServer || !forcePlayerOutFromSettlement) return;

        var settlement = PendingDefenderVictorySettlement;
        var encounter = PlayerEncounter.Current;
        if (settlement != null &&
            MobileParty.MainParty?.CurrentSettlement == settlement &&
            encounter?._mapEvent?.IsSiegeAssault == true &&
            encounter.EncounterState == PlayerEncounterState.End &&
            encounter.PlayerSide == BattleSideEnum.Defender)
        {
            forcePlayerOutFromSettlement = false;
        }
    }
}