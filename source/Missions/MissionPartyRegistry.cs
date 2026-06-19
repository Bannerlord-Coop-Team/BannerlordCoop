using Common.Logging;
using GameInterface.Services.Entity;
using Serilog;
using System;
using System.Collections.Generic;

namespace GameInterface.Missions;

/// <summary>
/// Source of truth for party ownership in a mission. Tracks who currently has authority over each party
/// (<see cref="MissionParty.CurrentAuthority"/>) and who it originally belongs to
/// (<see cref="MissionParty.OriginalOwner"/>), so authority can transfer to the host when an owner
/// disconnects and back to the owner when it rejoins.
/// </summary>
public interface IMissionPartyRegistry : IDisposable
{
    /// <summary>Registers a new party owned by <paramref name="originalOwner"/> (its initial authority).</summary>
    bool TryRegisterParty(Guid partyId, string originalOwner, Guid leaderAgentId, IEnumerable<Guid> troopAgentIds, out MissionParty party);
    bool TryGetParty(Guid partyId, out MissionParty party);
    /// <summary>Re-points a party's current authority (host on disconnect, owner on rejoin). OriginalOwner is unchanged.</summary>
    bool TryTransferAuthority(Guid partyId, string newAuthority);
    bool TryGetAuthority(Guid partyId, out string controllerId);
    /// <summary>True if the local controller currently has authority over the party.</summary>
    bool IsLocallyControlled(Guid partyId);
    /// <summary>Parties currently driven by <paramref name="controllerId"/> — used to hand off on disconnect.</summary>
    IReadOnlyList<MissionParty> GetPartiesControlledBy(string controllerId);
    /// <summary>Parties that belong to <paramref name="originalOwner"/> regardless of current authority — used to reclaim on rejoin.</summary>
    IReadOnlyList<MissionParty> GetPartiesOwnedBy(string originalOwner);
    bool RemoveParty(Guid partyId);
    void Clear();
}

/// <inheritdoc cref="IMissionPartyRegistry"/>
public class MissionPartyRegistry : IMissionPartyRegistry
{
    private static readonly ILogger Logger = LogManager.GetLogger<MissionPartyRegistry>();
    private readonly IControllerIdProvider controllerIdProvider;

    // Register / transfer / remove are cold (spawn, disconnect, rejoin) so a single lock around every
    // access keeps the three views consistent with each other.
    private readonly object gate = new();
    private readonly Dictionary<Guid, MissionParty> parties = new();
    private readonly Dictionary<string, HashSet<Guid>> byOriginalOwner = new();
    private readonly Dictionary<string, HashSet<Guid>> byCurrentAuthority = new();

    public MissionPartyRegistry(IControllerIdProvider controllerIdProvider)
    {
        this.controllerIdProvider = controllerIdProvider;
    }

    public void Dispose() => Clear();

    /// <inheritdoc/>
    public void Clear()
    {
        lock (gate)
        {
            parties.Clear();
            byOriginalOwner.Clear();
            byCurrentAuthority.Clear();
        }
    }

    /// <inheritdoc/>
    public bool TryRegisterParty(Guid partyId, string originalOwner, Guid leaderAgentId, IEnumerable<Guid> troopAgentIds, out MissionParty party)
    {
        party = null;

        if (partyId == Guid.Empty)
        {
            Logger.Error($"{nameof(partyId)} is empty.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(originalOwner))
        {
            Logger.Error($"{nameof(originalOwner)} is null or empty.");
            return false;
        }

        lock (gate)
        {
            if (parties.ContainsKey(partyId))
            {
                Logger.Error($"Party is already registered. PartyId: {partyId}");
                return false;
            }

            party = new MissionParty(partyId, originalOwner, leaderAgentId, troopAgentIds);
            parties[partyId] = party;
            Index(byOriginalOwner, originalOwner, partyId);
            Index(byCurrentAuthority, party.CurrentAuthority, partyId);
            return true;
        }
    }

    /// <inheritdoc/>
    public bool TryTransferAuthority(Guid partyId, string newAuthority)
    {
        if (string.IsNullOrWhiteSpace(newAuthority))
        {
            Logger.Error($"{nameof(newAuthority)} is null or empty.");
            return false;
        }

        lock (gate)
        {
            if (!parties.TryGetValue(partyId, out var party))
                return false;

            if (party.CurrentAuthority == newAuthority)
                return true;

            Deindex(byCurrentAuthority, party.CurrentAuthority, partyId);
            party.CurrentAuthority = newAuthority;
            Index(byCurrentAuthority, newAuthority, partyId);
            return true;
        }
    }

    /// <inheritdoc/>
    public bool TryGetParty(Guid partyId, out MissionParty party)
    {
        lock (gate)
        {
            return parties.TryGetValue(partyId, out party);
        }
    }

    /// <inheritdoc/>
    public bool TryGetAuthority(Guid partyId, out string controllerId)
    {
        controllerId = null;

        lock (gate)
        {
            if (!parties.TryGetValue(partyId, out var party))
                return false;

            controllerId = party.CurrentAuthority;
            return true;
        }
    }

    /// <inheritdoc/>
    public bool IsLocallyControlled(Guid partyId)
    {
        var localId = controllerIdProvider.ControllerId;

        lock (gate)
        {
            return parties.TryGetValue(partyId, out var party)
                && party.CurrentAuthority == localId;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<MissionParty> GetPartiesControlledBy(string controllerId) => Snapshot(byCurrentAuthority, controllerId);

    /// <inheritdoc/>
    public IReadOnlyList<MissionParty> GetPartiesOwnedBy(string originalOwner) => Snapshot(byOriginalOwner, originalOwner);

    /// <inheritdoc/>
    public bool RemoveParty(Guid partyId)
    {
        lock (gate)
        {
            if (!parties.TryGetValue(partyId, out var party))
                return false;

            parties.Remove(partyId);
            Deindex(byOriginalOwner, party.OriginalOwner, partyId);
            Deindex(byCurrentAuthority, party.CurrentAuthority, partyId);
            return true;
        }
    }

    private IReadOnlyList<MissionParty> Snapshot(Dictionary<string, HashSet<Guid>> index, string key)
    {
        var result = new List<MissionParty>();
        if (string.IsNullOrEmpty(key)) return result;

        lock (gate)
        {
            if (index.TryGetValue(key, out var ids))
            {
                foreach (var id in ids)
                {
                    if (parties.TryGetValue(id, out var party))
                        result.Add(party);
                }
            }
        }

        return result;
    }

    private static void Index(Dictionary<string, HashSet<Guid>> index, string key, Guid partyId)
    {
        if (!index.TryGetValue(key, out var ids))
        {
            ids = new HashSet<Guid>();
            index[key] = ids;
        }
        ids.Add(partyId);
    }

    private static void Deindex(Dictionary<string, HashSet<Guid>> index, string key, Guid partyId)
    {
        if (index.TryGetValue(key, out var ids) && ids.Remove(partyId) && ids.Count == 0)
            index.Remove(key);
    }
}
