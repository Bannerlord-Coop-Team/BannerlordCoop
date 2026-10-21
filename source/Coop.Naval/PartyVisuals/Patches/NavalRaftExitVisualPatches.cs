using Common.Logging;
using HarmonyLib;
using NavalDLC.View.Map.Managers;
using NavalDLC.View.Map.Visuals;
using SandBox.View;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.PartyVisuals.Patches;

/// <summary>
/// Drops a party's raft model when it leaves raft state, so its naval visual is rebuilt as a ship before it is ticked as one.
/// </summary>
[HarmonyPatch(typeof(MobileParty))]
internal class NavalRaftExitVisualPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalRaftExitVisualPatches>();

    // Vanilla only leaves a raft at landfall, where the naval visual stops ticking. Leaving it at sea with ships (the naval
    // scenario stager) ticked the raft entity as a ship, and TickFoamDecals hit foam decal slots only AddShipVisual creates.
    [HarmonyPatch(nameof(MobileParty.IsInRaftState), MethodType.Setter)]
    [HarmonyPostfix]
    private static void IsInRaftStateSetterPostfix(MobileParty __instance)
    {
        if (__instance.IsInRaftState) return;
        if (!TryGetNavalVisual(__instance, out var visual)) return;
        if (!ShouldDropRaftVisual(__instance, visual)) return;

        // ResetPartyIcon plus a dirty mark, so the next refresh runs AddShipVisual (or nothing while the fleet is gone)
        visual.ClearVisualMemory();
        Logger.Debug("[NavalRaft] Cleared the stale raft visual of {Party} after it left raft state", __instance.StringId);
    }

    /// <summary>Whether <paramref name="visual"/> still shows the raft of a party that has left raft state.</summary>
    internal static bool ShouldDropRaftVisual(MobileParty party, NavalMobilePartyVisual visual)
    {
        return !party.IsInRaftState && visual._isVisualInRaftState;
    }

    // The naval visual of the party, when a campaign map view exists (none on a headless instance or before the map loads).
    private static bool TryGetNavalVisual(MobileParty party, out NavalMobilePartyVisual visual)
    {
        visual = null;
        var visualManager = SandBoxViewSubModule._instance?._sandBoxViewVisualManager;
        if (visualManager == null || party.Party == null) return false;

        var navalVisualManager = visualManager.GetEntityComponent<NavalMobilePartyVisualManager>();
        if (navalVisualManager == null) return false;

        return navalVisualManager._partiesAndVisuals.TryGetValue(party.Party, out visual);
    }
}
