using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Mission-scoped network identities of naval hulls, keyed by <see cref="NetworkShipInfo.ShipId"/>.</summary>
public interface INetworkShipRegistry
{
    IReadOnlyList<NetworkShipInfo> Ships { get; }
    bool TryRegister(NetworkShipInfo ship);
    bool TryGet(Guid shipId, out NetworkShipInfo ship);
    bool TryGetByHull(MissionObject hull, out NetworkShipInfo ship);
    bool TryGetByFormation(Formation formation, out NetworkShipInfo ship);

    /// <summary>Points <paramref name="shipId"/> at a replacement hull, keeping its identity, authority and party.</summary>
    bool TryRebindHull(Guid shipId, MissionObject hull);

    bool Remove(Guid shipId);
}

/// <summary>One registered hull: who simulates it, who fielded it, and its local mission object and formation.</summary>
public class NetworkShipInfo
{
    public NetworkShipInfo(Guid shipId, string owner, string mapEventPartyId, bool isNpcParty, MissionObject hull, Formation formation)
    {
        ShipId = shipId;
        CurrentAuthority = owner;
        OriginalOwner = owner;
        MapEventPartyId = mapEventPartyId;
        IsNpcParty = isNpcParty;
        Hull = hull;
        Formation = formation;
    }

    public Guid ShipId { get; }
    public string CurrentAuthority { get; set; }
    public string OriginalOwner { get; }
    public string MapEventPartyId { get; }
    public bool IsNpcParty { get; }
    public MissionObject Hull { get; private set; }
    public Formation Formation { get; }

    internal void RebindHull(MissionObject hull) => Hull = hull;
}

/// <inheritdoc cref="INetworkShipRegistry"/>
public class NetworkShipRegistry : INetworkShipRegistry
{
    // Game thread only: hulls register on spawn and are read by the per-tick stream and spawn paths.
    private readonly Dictionary<Guid, NetworkShipInfo> ships = new Dictionary<Guid, NetworkShipInfo>();

    public IReadOnlyList<NetworkShipInfo> Ships => ships.Values.ToList();

    public bool TryRegister(NetworkShipInfo ship)
    {
        if (ship == null || ship.ShipId == Guid.Empty || ship.Hull == null || ships.ContainsKey(ship.ShipId)) return false;
        if (TryGetByHull(ship.Hull, out _)) return false;

        ships.Add(ship.ShipId, ship);
        return true;
    }

    public bool TryGet(Guid shipId, out NetworkShipInfo ship) => ships.TryGetValue(shipId, out ship);

    public bool TryGetByHull(MissionObject hull, out NetworkShipInfo ship)
    {
        ship = ships.Values.FirstOrDefault(entry => ReferenceEquals(entry.Hull, hull));
        return ship != null;
    }

    public bool TryGetByFormation(Formation formation, out NetworkShipInfo ship)
    {
        ship = formation == null ? null : ships.Values.FirstOrDefault(entry => ReferenceEquals(entry.Formation, formation));
        return ship != null;
    }

    public bool TryRebindHull(Guid shipId, MissionObject hull)
    {
        if (hull == null || !ships.TryGetValue(shipId, out var ship)) return false;
        if (TryGetByHull(hull, out var other) && other != ship) return false;

        ship.RebindHull(hull);
        return true;
    }

    public bool Remove(Guid shipId) => ships.Remove(shipId);
}
