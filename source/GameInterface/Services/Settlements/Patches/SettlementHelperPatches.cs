using Common;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// [Client] Vanilla's garrison morale path passes MobileParty.CurrentSettlement straight into the
/// starving check with no null guard; a half-replicated or locally-mangled garrison has none, and
/// the check runs inside mission-spawn and per-frame paths where the NRE kills the client.
/// </summary>
[HarmonyPatch(typeof(SettlementHelper), nameof(SettlementHelper.IsGarrisonStarving))]
internal class SettlementHelperPatches
{
    [HarmonyPrefix]
    private static bool IsGarrisonStarvingPrefix(Settlement settlement, ref bool __result)
    {
        if (ModInformation.IsServer) return true;
        if (settlement != null) return true;

        __result = false;
        return false;
    }
}
