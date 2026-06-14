using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Keeps a map event's battle icon visibility correct on the client.
/// </summary>
/// <remarks>
/// Map-event icon visibility is a per-machine concern: vanilla derives it from whether any involved
/// party is visible to the local main party's see range, so it must be computed locally rather than
/// synced. The dedicated server has no main party and force-marks every party visible, so it would only
/// ever report a battle as visible; that value is therefore not synced (see <see cref="MapEventSync"/>),
/// and each client reconciles its own icons here instead.
///
/// The vanilla party-visibility chain (PartyBase.OnVisibilityChanged -> MapEvent.PartyVisibilityChanged)
/// only fires on visibility transitions, which never happen for battles that stay outside the client's
/// view-update range or that begin with already-visible parties. Reconciling every real tick covers both
/// cases, runs right after the vanilla visibility pass (so it reads fresh party state), and is gated to
/// only touch the icon when it actually changes. Runs on the client only; the server's authoritative
/// behaviour is unchanged.
/// </remarks>
[HarmonyPatch(typeof(Campaign))]
internal class MapEventVisibilityClientPatch
{
    // MapEvent.IsVisible has a private setter; driving it (rather than the backing field) preserves the
    // vanilla side effect that pushes the new visibility to the map-event visual / battle icon. Cached as
    // a compiled delegate so the per-change call avoids reflection and argument boxing.
    private static readonly Action<MapEvent, bool> SetIsVisible =
        AccessTools.MethodDelegate<Action<MapEvent, bool>>(
            AccessTools.PropertySetter(typeof(MapEvent), nameof(MapEvent.IsVisible)));

    [HarmonyPatch(nameof(Campaign.RealTick))]
    [HarmonyPostfix]
    private static void Postfix_RealTick()
    {
        if (!ModInformation.IsClient) return;

        var mapEventManager = Campaign.Current?.MapEventManager;
        if (mapEventManager == null) return;

        foreach (var mapEvent in mapEventManager.MapEvents)
        {
            // Skip events whose sides are not both assigned yet. On the client a map event is created
            // first and its sides are wired up afterwards via separate messages, so during that window
            // enumerating InvolvedParties would dereference an unassigned side.
            if (mapEvent?.AttackerSide == null || mapEvent.DefenderSide == null) continue;

            bool shouldBeVisible = AnyInvolvedPartyVisible(mapEvent);
            if (mapEvent.IsVisible != shouldBeVisible)
                SetIsVisible(mapEvent, shouldBeVisible);
        }
    }

    // Mirrors vanilla MapEvent.PartyVisibilityChanged: the icon is visible while any involved party is.
    private static bool AnyInvolvedPartyVisible(MapEvent mapEvent)
    {
        foreach (var party in mapEvent.InvolvedParties)
        {
            if (party != null && party.IsVisible)
                return true;
        }

        return false;
    }
}
