using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Missions.Battles;

/// <summary>
/// [Game thread] What arrived for ships not registered here yet: each one's waiting spawn record and its owner's latest
/// condition, sample and final ropes, applied in that order once the copy spawns.
/// </summary>
public interface IHeldShipBuffer
{
    /// <summary>Every ship something is held for.</summary>
    IEnumerable<HeldShip> All { get; }

    /// <summary>The held state of a ship, created on first use.</summary>
    HeldShip Of(Guid shipId);

    /// <summary>The held state of a ship, if anything is held for it.</summary>
    bool TryGet(Guid shipId, out HeldShip held);

    /// <summary>Whether a spawn record for the ship waits here, so its owner's later traffic is held with it.</summary>
    bool IsAwaitingRecord(Guid shipId);

    /// <summary>Holds <paramref name="record"/> as the ship's waiting record, behind every record that arrived before it.</summary>
    void HoldRecord(HeldShip held, BattleShipSpawnData record);

    /// <summary>The ships whose records wait, in the order the records arrived.</summary>
    IReadOnlyList<HeldShip> WaitingRecords();

    /// <summary>Drops everything held for a ship.</summary>
    void Remove(Guid shipId);

    /// <summary>Drops everything held, as the mission ends.</summary>
    void Clear();
}

/// <inheritdoc cref="IHeldShipBuffer"/>
public class HeldShipBuffer : IHeldShipBuffer
{
    private readonly Dictionary<Guid, HeldShip> ships = new Dictionary<Guid, HeldShip>();
    private long recordsHeld;

    public IEnumerable<HeldShip> All => ships.Values;

    public HeldShip Of(Guid shipId)
    {
        if (!ships.TryGetValue(shipId, out var held))
        {
            held = new HeldShip(shipId);
            ships.Add(shipId, held);
        }

        return held;
    }

    public bool TryGet(Guid shipId, out HeldShip held) => ships.TryGetValue(shipId, out held);

    public bool IsAwaitingRecord(Guid shipId) => ships.TryGetValue(shipId, out var held) && held.Record != null;

    public void HoldRecord(HeldShip held, BattleShipSpawnData record)
    {
        held.Record = record;
        held.RecordOrder = ++recordsHeld;
    }

    public IReadOnlyList<HeldShip> WaitingRecords() =>
        ships.Values.Where(held => held.Record != null).OrderBy(held => held.RecordOrder).ToArray();

    public void Remove(Guid shipId) => ships.Remove(shipId);

    public void Clear() => ships.Clear();
}

/// <summary>
/// A ship not registered here yet: its waiting spawn record and the owner's latest condition, sample and final ropes, with
/// the record's spawn attempts and why it waits.
/// </summary>
public sealed class HeldShip
{
    // Starts empty for one ship id.
    public HeldShip(Guid shipId)
    {
        ShipId = shipId;
    }

    public Guid ShipId { get; }
    public BattleShipSpawnData Record;
    /// <summary>The record's place in arrival order, so copies spawn in the order their records came.</summary>
    public long RecordOrder;
    public NetworkShipCondition Condition;
    public NetworkBattleShipSample Sample;
    public NetworkBattleRopeFinal FinalRopes;
    public int FailedAttempts;
    public float RetryAt;
    public bool HeldForDeployment;
    public bool HeldForMembership;

    /// <summary>Keeps <paramref name="condition"/> unless the held one is newer.</summary>
    public void HoldCondition(NetworkShipCondition condition)
    {
        if (Condition == null || condition.Stamp.IsNewer(Condition.Stamp)) Condition = condition;
    }

    /// <summary>Keeps <paramref name="sample"/> unless the held one is newer.</summary>
    public void HoldSample(NetworkBattleShipSample sample)
    {
        if (Sample == null || sample.Stamp.IsNewer(Sample.Stamp)) Sample = sample;
    }

    /// <summary>The record spawned or was dropped: no record waits, and its attempts and holds reset.</summary>
    public void SettleRecord()
    {
        Record = null;
        FailedAttempts = 0;
        HeldForDeployment = false;
        HeldForMembership = false;
    }
}
