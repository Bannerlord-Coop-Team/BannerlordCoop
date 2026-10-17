using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Keeps a winning inside siege defender in its town when the after battle flow finishes its encounter. That flow
/// ends with <c>PlayerEncounter.Finish(true)</c>, which (after finalizing the battle) leaves the settlement the
/// main party is in. On a client that leave goes to the server, which ejects the defender and closes the siege
/// defeated menu. The server keeps the winner inside, and SiegeDefenderVictoryHandler seats it on
/// siege_attacker_defeated once the encounter is finished.
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