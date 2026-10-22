using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>Map events still referenced by parties, split by whether they are finalized and listed.</summary>
internal sealed class OrphanedMapEvents
{
    private readonly Dictionary<MapEvent, List<PartyBase>> linkedParties;

    public OrphanedMapEvents(
        IReadOnlyList<MapEvent> finalized,
        IReadOnlyList<MapEvent> unlistedLive,
        Dictionary<MapEvent, List<PartyBase>> linkedParties)
    {
        Finalized = finalized;
        UnlistedLive = unlistedLive;
        this.linkedParties = linkedParties;
    }

    /// <summary>Finalized events a party still points at, listed in the manager or not.</summary>
    public IReadOnlyList<MapEvent> Finalized { get; }

    /// <summary>Live events a party points at that the manager does not list.</summary>
    public IReadOnlyList<MapEvent> UnlistedLive { get; }

    public IReadOnlyList<PartyBase> LinkedParties(MapEvent mapEvent) =>
        linkedParties.TryGetValue(mapEvent, out var parties) ? parties : new List<PartyBase>();
}

internal interface IOrphanedMapEventFinder
{
    OrphanedMapEvents Find(IEnumerable<PartyBase> parties, ISet<MapEvent> listed);
}

internal sealed class OrphanedMapEventFinder : IOrphanedMapEventFinder
{
    public OrphanedMapEvents Find(IEnumerable<PartyBase> parties, ISet<MapEvent> listed)
    {
        var linkedParties = new Dictionary<MapEvent, List<PartyBase>>();
        var order = new List<MapEvent>();

        foreach (var party in parties)
        {
            var mapEvent = party?._mapEventSide?._mapEvent;
            if (mapEvent == null)
                continue;

            if (!linkedParties.TryGetValue(mapEvent, out var linked))
            {
                linked = new List<PartyBase>();
                linkedParties.Add(mapEvent, linked);
                order.Add(mapEvent);
            }

            linked.Add(party);
        }

        var finalized = order.Where(mapEvent => mapEvent.IsFinalized).ToList();
        var unlistedLive = order.Where(mapEvent => !mapEvent.IsFinalized && !listed.Contains(mapEvent)).ToList();
        return new OrphanedMapEvents(finalized, unlistedLive, linkedParties);
    }
}
