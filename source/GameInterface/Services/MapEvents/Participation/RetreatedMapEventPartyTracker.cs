using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Participation;

/// <inheritdoc cref="IRetreatedMapEventPartyTracker"/>
internal sealed class RetreatedMapEventPartyTracker : IRetreatedMapEventPartyTracker
{
    private readonly object gate = new();
    private readonly Dictionary<MapEvent, HashSet<PartyBase>> retreatedParties = new(ReferenceComparer<MapEvent>.Instance);

    public void MarkRetreated(MapEvent mapEvent, PartyBase party)
    {
        if (mapEvent == null || party == null) return;

        lock (gate)
        {
            if (!retreatedParties.TryGetValue(mapEvent, out var parties))
            {
                parties = new HashSet<PartyBase>(ReferenceComparer<PartyBase>.Instance);
                retreatedParties.Add(mapEvent, parties);
            }

            parties.Add(party);
        }
    }

    public void MarkReentered(MapEvent mapEvent, PartyBase party)
    {
        if (mapEvent == null || party == null) return;

        lock (gate)
        {
            if (!retreatedParties.TryGetValue(mapEvent, out var parties)) return;

            parties.Remove(party);
            if (parties.Count == 0) retreatedParties.Remove(mapEvent);
        }
    }

    public bool IsRetreated(MapEvent mapEvent, PartyBase party)
    {
        if (mapEvent == null || party == null) return false;

        lock (gate) return retreatedParties.TryGetValue(mapEvent, out var parties) && parties.Contains(party);
    }

    public void Clear(MapEvent mapEvent)
    {
        if (mapEvent == null) return;

        lock (gate) retreatedParties.Remove(mapEvent);
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static ReferenceComparer<T> Instance { get; } = new();

        public bool Equals(T x, T y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}