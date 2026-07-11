using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Initialization;

/// <summary>
/// Defines the complete registry-owned object graph rooted at a MapEvent. The same traversal is used
/// for aggregate publication and for adopting pre-existing save/load graphs into atomic teardown.
/// </summary>
internal static class MapEventGraph
{
    public static IEnumerable<object> Enumerate(MapEvent mapEvent)
    {
        if (mapEvent == null) throw new ArgumentNullException(nameof(mapEvent));

        yield return mapEvent;
        yield return mapEvent.Component;
        yield return mapEvent.TroopUpgradeTracker;
        yield return mapEvent.MapEventVisual;

        if (mapEvent._sides == null) yield break;

        foreach (var side in mapEvent._sides)
        {
            yield return side;
            if (side?.Parties == null) continue;

            foreach (var party in side.Parties)
            {
                foreach (var instance in EnumerateParty(party))
                    yield return instance;
            }
        }
    }

    public static IEnumerable<object> EnumerateParty(MapEventParty party)
    {
        yield return party;
        if (party == null) yield break;

        yield return party._woundedInBattle;
        yield return party._diedInBattle;
        yield return party._routedInBattle;
    }
}
