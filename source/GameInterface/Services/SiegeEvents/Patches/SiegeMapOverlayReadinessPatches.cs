using Common;
using GameInterface.Services.SiegeEngines;
using HarmonyLib;
using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Defers the client's campaign-map siege overlay build until the replicated besieger-camp graph is complete.
/// The camp's LeaderParty and both sides' SiegeEnginesContainers (with their Deployed engine arrays) arrive as
/// separate AutoSync sets that drain over a few game-thread frames after the siege becomes the player's. Vanilla's
/// <c>SettlementVisualManager.RefreshMapSiegeOverlayRequired</c> builds the MapSiege overlay exactly once, the frame
/// <c>PlayerSiege.PlayerSiegeEvent</c> turns non-null; if that frame lands mid-sync the MapSiege POI view-models
/// construct against a null BesiegerCamp/SiegeEngines and the engine-build tiles come up dead for the whole siege.
/// Skipping the build while the graph is incomplete leaves the overlay unbuilt, so vanilla retries next frame and
/// builds it correctly once the camp is whole.
/// </summary>
[HarmonyPatch]
internal static class SiegeMapOverlayReadinessPatches
{
    [HarmonyPatch(typeof(SettlementVisualManager), "RefreshMapSiegeOverlayRequired")]
    [HarmonyPrefix]
    private static bool RefreshMapSiegeOverlayRequiredPrefix()
    {
        if (ModInformation.IsServer) return true;

        var siege = PlayerSiege.PlayerSiegeEvent;
        // No player siege: let vanilla run (it tears the overlay down). A half-synced camp: skip this frame so
        // the one-time build waits for the complete graph rather than baking dead tiles.
        if (siege != null && !SiegeContainerLookup.IsGraphComplete(siege)) return false;
        return true;
    }
}
