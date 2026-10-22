using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
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

    /// <summary>
    /// Records the prize of a capture as vanilla's swap leaves it: it crews <paramref name="formation"/> and follows its captor,
    /// at the hull's capture <paramref name="sequence"/>.
    /// </summary>
    bool TryMarkCaptured(Guid shipId, HullCapture capture, Formation formation, int sequence);

    /// <summary>
    /// Records a hull a capture left without a formation or team, last on <paramref name="side"/>, at the hull's capture
    /// <paramref name="sequence"/>.
    /// </summary>
    bool TryMarkOrphaned(Guid shipId, BattleSideEnum side, int sequence);

    /// <summary>
    /// Lets a ship id that left the mission with a departing player register again, for a captor's record of a hull its earlier
    /// owner took out only here.
    /// </summary>
    bool TryReopen(Guid shipId);

    /// <summary>Drops a hull that left the mission; its ship id reads <see cref="ShipLifecycle.Removed"/> from then on.</summary>
    bool Remove(Guid shipId);

    /// <summary>Records a ship id that left the mission before its hull registered here, so it never spawns or buffers again.</summary>
    void MarkRemoved(Guid shipId);

    /// <summary>Records another owner's hull whose spawn record waits here; registering it, or giving up on it, ends the wait.</summary>
    void MarkAwaiting(Guid shipId);

    /// <summary>Records a foreign hull this client gave up spawning, so its buffered crew is dropped; registering it clears this.</summary>
    void MarkSpawnFailed(Guid shipId);

    /// <summary>[Game thread] Where a ship id is in its local lifecycle, polled by every buffer that waits for a hull.</summary>
    ShipLifecycle StateOf(Guid shipId);

    /// <summary>
    /// [Game thread] Whether traffic for a ship id that is not registered can be dropped: its spawn was given up or it left the
    /// mission. The one check every buffer that waits for a hull uses.
    /// </summary>
    bool IsGone(Guid shipId);
}

/// <summary>A ship id's local lifecycle: its spawn record waits, it is registered, its spawn was given up, or it left the mission.</summary>
public enum ShipLifecycle
{
    Unknown,
    Awaiting,
    Registered,
    SpawnFailed,
    Removed,
}

/// <summary>One registered hull: who simulates it, who fielded it, who captured it, and its local mission object and formation.</summary>
public class NetworkShipInfo
{
    public NetworkShipInfo(Guid shipId, string owner, string mapEventPartyId, bool isNpcParty, MissionObject hull, Formation formation,
        string originalOwner = null)
    {
        ShipId = shipId;
        CurrentAuthority = owner;
        OriginalOwner = originalOwner ?? owner;
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

    /// <summary>The hull's last capture as a prize: who took it, with which hull, from which side; null before any.</summary>
    public HullCapture? Capture { get; private set; }

    /// <summary>The player whose capture made this hull its own, or null; a captured AI hull no longer follows the host.</summary>
    public string CaptorControllerId => Capture?.CaptorControllerId;

    /// <summary>How many captures this hull took part in, as prize or as the captor's hull; confirms apply in this order.</summary>
    public int CaptureSequence { get; private set; }

    /// <summary>The side a hull was on before a capture left it with no formation or team; none while it has one.</summary>
    public BattleSideEnum OrphanedSide { get; private set; } = BattleSideEnum.None;

    /// <summary>A hull whose formation a capture gave to the prize.</summary>
    public bool IsOrphaned => OrphanedSide != BattleSideEnum.None;

    /// <summary>An AI hull nobody captured: it moves with every battle host change, and its states follow the host epoch.</summary>
    public bool FollowsBattleHost => IsNpcParty && Capture == null;

    public MissionObject Hull { get; private set; }
    public Formation Formation { get; private set; }

    // Points the identity at a replacement hull.
    internal void RebindHull(MissionObject hull) => Hull = hull;

    // The prize of a capture: it crews the captor's formation from now on.
    internal void MarkCaptured(HullCapture capture, Formation formation, int sequence)
    {
        Capture = capture;
        Formation = formation;
        OrphanedSide = BattleSideEnum.None;
        CaptureSequence = sequence;
    }

    // The captor's own hull after its capture: vanilla leaves it with no formation and no team.
    internal void MarkOrphaned(BattleSideEnum side, int sequence)
    {
        Formation = null;
        OrphanedSide = side;
        CaptureSequence = sequence;
    }
}

/// <summary>One capture of a hull as its prize: the captor, the captor's own hull that lost its formation, and the side it was taken from.</summary>
public readonly struct HullCapture
{
    public HullCapture(string captorControllerId, Guid captorShipId, BattleSideEnum fromSide)
    {
        CaptorControllerId = captorControllerId;
        CaptorShipId = captorShipId;
        FromSide = fromSide;
    }

    public string CaptorControllerId { get; }
    public Guid CaptorShipId { get; }
    public BattleSideEnum FromSide { get; }
}

/// <inheritdoc cref="INetworkShipRegistry"/>
public class NetworkShipRegistry : INetworkShipRegistry
{
    // Game thread only: hulls register on spawn and are read by the per-tick stream and spawn paths.
    private readonly Dictionary<Guid, NetworkShipInfo> ships = new Dictionary<Guid, NetworkShipInfo>();
    // Registered ship ids are read from ships; this holds every other state a ship id reached.
    private readonly Dictionary<Guid, ShipLifecycle> unregistered = new Dictionary<Guid, ShipLifecycle>();

    public IReadOnlyList<NetworkShipInfo> Ships => ships.Values.ToList();

    public bool TryRegister(NetworkShipInfo ship)
    {
        if (ship == null || ship.ShipId == Guid.Empty || ship.Hull == null || ships.ContainsKey(ship.ShipId)) return false;
        if (TryGetByHull(ship.Hull, out _)) return false;

        ships.Add(ship.ShipId, ship);
        unregistered.Remove(ship.ShipId);
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

    public bool TryMarkCaptured(Guid shipId, HullCapture capture, Formation formation, int sequence)
    {
        if (!ships.TryGetValue(shipId, out var ship)) return false;

        ship.MarkCaptured(capture, formation, sequence);
        return true;
    }

    public bool TryMarkOrphaned(Guid shipId, BattleSideEnum side, int sequence)
    {
        if (side == BattleSideEnum.None || !ships.TryGetValue(shipId, out var ship)) return false;

        ship.MarkOrphaned(side, sequence);
        return true;
    }

    public bool TryReopen(Guid shipId)
    {
        if (!unregistered.TryGetValue(shipId, out var state) || state != ShipLifecycle.Removed) return false;

        unregistered.Remove(shipId);
        return true;
    }

    public bool Remove(Guid shipId)
    {
        if (!ships.Remove(shipId)) return false;

        unregistered[shipId] = ShipLifecycle.Removed;
        return true;
    }

    public void MarkAwaiting(Guid shipId) => MarkUnregistered(shipId, ShipLifecycle.Awaiting);

    public void MarkSpawnFailed(Guid shipId) => MarkUnregistered(shipId, ShipLifecycle.SpawnFailed);

    public void MarkRemoved(Guid shipId) => MarkUnregistered(shipId, ShipLifecycle.Removed);

    // A ship that left the mission stays Removed: a late record or retry must not bring it back.
    private void MarkUnregistered(Guid shipId, ShipLifecycle state)
    {
        if (shipId == Guid.Empty || ships.ContainsKey(shipId)) return;
        if (unregistered.TryGetValue(shipId, out var current) && current == ShipLifecycle.Removed) return;

        unregistered[shipId] = state;
    }

    public ShipLifecycle StateOf(Guid shipId)
    {
        if (ships.ContainsKey(shipId)) return ShipLifecycle.Registered;
        return unregistered.TryGetValue(shipId, out var state) ? state : ShipLifecycle.Unknown;
    }

    public bool IsGone(Guid shipId)
    {
        var state = StateOf(shipId);
        return state == ShipLifecycle.SpawnFailed || state == ShipLifecycle.Removed;
    }
}
