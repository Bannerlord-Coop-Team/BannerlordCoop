using GameInterface.Services.Locations.Messages;
using Missions.Messages;
using System;
using System.Collections.Concurrent;

namespace Missions.Locations;

/// <summary>
/// What we know about each replicated settlement NPC, keyed by its network agent id: its kind, its
/// roster identity (humans, SR-022) and item identities (animals). Populated on host capture AND on
/// puppet spawn (every client), so ANY future host can rebuild records for catch-up and re-bind
/// adopted puppets to local roster entries (SR-030). Also the player-vs-NPC discriminator: player
/// agents are never recorded here.
/// </summary>
public interface ILocationAgentBindingMap
{
    void Record(Guid agentId, LocationAgentBinding binding);
    bool TryGet(Guid agentId, out LocationAgentBinding binding);
    void Forget(Guid agentId);

    /// <summary>True when any NPC binding exists — a client holding bindings it did not capture is
    /// holding puppets, which is how a promoted host tells adoption from initial population.</summary>
    int Count { get; }
}

public sealed class LocationAgentBinding
{
    public LocationAgentKind Kind { get; }

    /// <summary>Roster identity for humans; null for animals and unbound humans.</summary>
    public LocationCharacterData RosterEntry { get; }

    /// <summary>Animal item ids (ObjectManager); null for humans.</summary>
    public string ItemId { get; }
    public string HarnessItemId { get; }

    public LocationAgentBinding(LocationAgentKind kind, LocationCharacterData rosterEntry, string itemId = null, string harnessItemId = null)
    {
        Kind = kind;
        RosterEntry = rosterEntry;
        ItemId = itemId;
        HarnessItemId = harnessItemId;
    }
}

/// <inheritdoc cref="ILocationAgentBindingMap"/>
public class LocationAgentBindingMap : ILocationAgentBindingMap
{
    private readonly ConcurrentDictionary<Guid, LocationAgentBinding> bindings = new();

    public void Record(Guid agentId, LocationAgentBinding binding)
    {
        if (agentId == Guid.Empty || binding == null) return;
        bindings[agentId] = binding;
    }

    public bool TryGet(Guid agentId, out LocationAgentBinding binding)
        => bindings.TryGetValue(agentId, out binding);

    public void Forget(Guid agentId) => bindings.TryRemove(agentId, out _);

    public int Count => bindings.Count;
}
