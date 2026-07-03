using System;
using System.Collections.Concurrent;

namespace Missions.Battles;

/// <summary>
/// Casualty attribution for one battle agent, captured at spawn: the map-event party the troop fights for,
/// the exact troop descriptor seed (feeds the puppet origin on replay), and the troop's CharacterObject
/// object-manager id — what the server keys the roster casualty on (descriptor seeds churn, see
/// <c>NetworkRequestBattleCasualty</c>).
/// </summary>
public readonly struct CasualtyAttribution
{
    public string MapEventPartyId { get; }
    public int TroopSeed { get; }
    public string TroopCharacterId { get; }

    public CasualtyAttribution(string mapEventPartyId, int troopSeed, string troopCharacterId)
    {
        MapEventPartyId = mapEventPartyId;
        TroopSeed = troopSeed;
        TroopCharacterId = troopCharacterId;
    }
}

/// <summary>
/// Per-agent casualty attribution captured at spawn and read on death, when the agent's owner reports the
/// casualty to the server. Written from the main thread (own-spawn capture) and the network thread (peer
/// puppet spawn), read on death — hence concurrent.
/// </summary>
public interface ICasualtyAttributionMap
{
    void Record(Guid agentId, string mapEventPartyId, int troopSeed, string troopCharacterId);

    /// <summary>The recorded attribution, or an empty default (null party/character ids) when none was captured.</summary>
    CasualtyAttribution GetOrDefault(Guid agentId);

    void Forget(Guid agentId);
}

/// <inheritdoc cref="ICasualtyAttributionMap"/>
public class CasualtyAttributionMap : ICasualtyAttributionMap
{
    private readonly ConcurrentDictionary<Guid, CasualtyAttribution> attributions = new();

    public void Record(Guid agentId, string mapEventPartyId, int troopSeed, string troopCharacterId)
        => attributions[agentId] = new CasualtyAttribution(mapEventPartyId, troopSeed, troopCharacterId);

    public CasualtyAttribution GetOrDefault(Guid agentId)
        => attributions.TryGetValue(agentId, out var attribution) ? attribution : default;

    public void Forget(Guid agentId) => attributions.TryRemove(agentId, out _);
}
