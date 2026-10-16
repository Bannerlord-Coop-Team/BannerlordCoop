using Missions.Messages;
using System;
using System.Collections.Generic;

namespace Missions.Battles;

/// <summary>
/// [Game thread] The damage state of each registered hull. Its owner sends a hull's condition when it changes, and copies
/// take on the owner's in <see cref="AuthorityStamp"/> order, so HP, fire and sinking follow the one client that applies
/// damage to the hull.
/// </summary>
public interface IBattleShipConditionStream
{
    /// <summary>The owner's last sent, or a copy's last applied, condition of a hull; null before the first.</summary>
    BattleShipCondition ConditionOf(Guid shipId);

    /// <summary>The revision of <see cref="ConditionOf"/>, 0 before the first (diagnostics).</summary>
    long RevisionOf(Guid shipId);

    /// <summary>[Owner] Sends the hull's condition when it differs from the last one sent; the first check sends it once.</summary>
    void SendIfChanged(NetworkShipInfo ship);

    /// <summary>
    /// Applies an owner's condition to its registered copy; why it was dropped, or null. A departed host's held condition is
    /// behind on the host epoch by design, so it is taken without <paramref name="checkHostEpoch"/>.
    /// </summary>
    string Accept(NetworkShipInfo ship, NetworkShipCondition condition, bool checkHostEpoch);

    /// <summary>Drops a hull that left the mission.</summary>
    void Forget(Guid shipId);
}

/// <inheritdoc cref="IBattleShipConditionStream"/>
public class BattleShipConditionStream : IBattleShipConditionStream
{
    private readonly IBattleNetwork network;
    private readonly IBattleSession session;
    private readonly INavalShipEngine engine;
    private readonly IHostEpochPolicy hostEpochPolicy;

    private readonly Dictionary<Guid, HullCondition> hulls = new Dictionary<Guid, HullCondition>();

    public BattleShipConditionStream(IBattleNetwork network, IBattleSession session, INavalShipEngine engine,
        IHostEpochPolicy hostEpochPolicy)
    {
        this.network = network;
        this.session = session;
        this.engine = engine;
        this.hostEpochPolicy = hostEpochPolicy;
    }

    public BattleShipCondition ConditionOf(Guid shipId) => hulls.TryGetValue(shipId, out var hull) ? hull.Condition : null;

    public long RevisionOf(Guid shipId) => hulls.TryGetValue(shipId, out var hull) ? hull.Revision : 0;

    public void SendIfChanged(NetworkShipInfo ship)
    {
        var hull = Of(ship.ShipId);
        var condition = engine.ReadCondition(ship.Hull);
        if (condition == null || !condition.DiffersFrom(hull.Condition)) return;

        hull.Condition = condition;
        hull.Revision++;
        hull.Epoch = session.HostEpoch;
        network.SendAll(new NetworkShipCondition(ship.ShipId, session.OwnControllerId, hull.Revision, session.HostEpoch, condition));
    }

    public string Accept(NetworkShipInfo ship, NetworkShipCondition condition, bool checkHostEpoch)
    {
        var hull = Of(ship.ShipId);
        var rejection = ValidateCondition(ship, session.OwnControllerId, condition, hull.Revision, hull.Epoch);
        if (rejection == null && checkHostEpoch && ship.IsNpcParty && hostEpochPolicy.IsStale(condition.HostEpoch, session.HostEpoch))
            rejection = "stale_epoch";
        if (rejection != null) return rejection;

        engine.ApplyCondition(ship.Hull, condition.Condition);
        hull.Condition = condition.Condition;
        hull.Revision = condition.Revision;
        if (ship.IsNpcParty) hull.Epoch = condition.HostEpoch;
        return null;
    }

    /// <summary>Why a condition must be dropped, or null to apply it.</summary>
    internal static string ValidateCondition(NetworkShipInfo ship, string ownControllerId, NetworkShipCondition condition,
        long acceptedRevision, int acceptedEpoch)
    {
        // A new host numbers an AI hull's conditions on from its own count, so a newer epoch restarts the order.
        var rejection = condition.Stamp.Validate(ship.CurrentAuthority, ownControllerId,
            new AuthorityStamp(ship.CurrentAuthority, acceptedEpoch, acceptedRevision), followsHostEpoch: ship.IsNpcParty);
        if (rejection != null) return rejection;
        if (condition.Condition == null || !condition.Condition.IsValid) return "invalid_condition";
        return null;
    }

    public void Forget(Guid shipId) => hulls.Remove(shipId);

    private HullCondition Of(Guid shipId)
    {
        if (!hulls.TryGetValue(shipId, out var hull))
        {
            hull = new HullCondition();
            hulls.Add(shipId, hull);
        }

        return hull;
    }

    /// <summary>The owner's last sent, or a copy's last applied, condition, its revision and its host epoch.</summary>
    private sealed class HullCondition
    {
        public BattleShipCondition Condition;
        public long Revision;
        public int Epoch;
    }
}
