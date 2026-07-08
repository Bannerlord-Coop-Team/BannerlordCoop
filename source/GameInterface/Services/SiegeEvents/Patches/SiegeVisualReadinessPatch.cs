using Common;
using HarmonyLib;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Defers the client's besieged-settlement map visual refresh until the replicated siege graph is whole.
/// The BesiegerCamp, both sides' SiegeEnginesContainers and their Deployed arrays arrive as separate AutoSync
/// sets over a few frames after the siege becomes the player's. Vanilla's RefreshPartyIcon dereferences that
/// graph (AddSiegeIconComponents, RefreshSiegePreparations) and NREs while it is half-synced - and because it
/// clears the visual-dirty flag before it throws, the refresh never re-runs, so the siege visuals stay stale.
/// Skipping the refresh while the graph is incomplete keeps the visual dirty, so vanilla rebuilds it once whole.
/// </summary>
[HarmonyPatch(typeof(SettlementVisual), "RefreshPartyIcon")]
internal class SiegeVisualReadinessPatch
{
    [HarmonyPrefix]
    private static bool Prefix(SettlementVisual __instance)
    {
        if (ModInformation.IsServer) return true;

        var settlement = __instance?.MapEntity?.Settlement;
        if (settlement == null || !settlement.IsUnderSiege) return true;

        return IsSiegeGraphReady(settlement);
    }

    private static bool IsSiegeGraphReady(Settlement settlement)
    {
        var attacker = settlement.SiegeEvent?.BesiegerCamp?.SiegeEngines;
        if (attacker?.DeployedRangedSiegeEngines == null || attacker.DeployedMeleeSiegeEngines == null) return false;

        var defender = settlement.SiegeEngines;
        return defender?.DeployedRangedSiegeEngines != null && defender.DeployedMeleeSiegeEngines != null;
    }
}
