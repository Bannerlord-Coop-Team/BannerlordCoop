using Common;
using HarmonyLib;
using System;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Keeps a map event's battle icon visibility correct on the client.
/// </summary>
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
        if (ModInformation.IsServer) return;

        var mapEventManager = Campaign.Current?.MapEventManager;
        if (mapEventManager == null) return;

        var mainParty = MobileParty.MainParty;
        if (mainParty == null) return;

        float seeingRange = mainParty.SeeingRange;
        if (seeingRange <= 0f) return;

        var mainPosition = mainParty.Position.ToVec2();
        float seeingRangeSquared = seeingRange * seeingRange;

        foreach (var mapEvent in mapEventManager.MapEvents)
        {
            if (mapEvent == null) continue;

            // Involved-party IsVisible cannot be trusted here: the server forces every party visible
            // (PartyVisibilityServerPatches) and vanilla only refreshes visibility for parties near the
            // local main party, so distant parties stay visible forever on the client. Clamp the icon
            // by distance to the local player instead, mirroring PartyNameplateVisibilityPatch.
            bool shouldBeVisible = mapEvent.IsPlayerMapEvent ||
                mapEvent.Position.ToVec2().DistanceSquared(mainPosition) <= seeingRangeSquared;

            if (mapEvent.IsVisible != shouldBeVisible)
                SetIsVisible(mapEvent, shouldBeVisible);
        }
    }
}
