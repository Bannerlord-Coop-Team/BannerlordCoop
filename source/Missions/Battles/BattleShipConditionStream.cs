using Missions.Messages;
using Missions.Services.Network;
using System;
using System.Collections.Generic;

namespace Missions.Battles;

/// <summary>
/// [Game thread] The damage state and fieldable reserve of each registered hull. Its owner sends a hull's condition when
/// either changes, and copies take on the owner's in <see cref="AuthorityStamp"/> order, so HP, fire, sinking and the reserve
/// follow the one client that simulates the hull.
/// </summary>
public interface IBattleShipConditionStream
{
    /// <summary>The owner's last sent, or a copy's last applied, condition of a hull; null before the first.</summary>
    BattleShipCondition ConditionOf(Guid shipId);

    /// <summary>The revision of <see cref="ConditionOf"/>, 0 before the first (diagnostics).</summary>
    long RevisionOf(Guid shipId);

    /// <summary>
    /// The fieldable reserve the hull's owner last reported with its condition, or null once forgotten or before the first;
    /// only the owner tracks the hull's reserve queue.
    /// </summary>
    int? ReportedReserveOf(Guid shipId);

    /// <summary>
    /// [Owner] Sends the hull's condition and fieldable reserve when either differs from the last one sent; the first check
    /// sends once.
    /// </summary>
    void SendIfChanged(NetworkShipInfo ship);

    /// <summary>[Owner] Sends a peer that entered late the hull's last sent condition and reserve, with the same stamp.</summary>
    void SendLastTo(string controllerId, NetworkShipInfo ship);

    /// <summary>
    /// Applies an owner's condition to its registered copy; why it was dropped, or null. A departed host's held condition is
    /// behind on the host epoch and from a controller no longer in the mission by design, so it is taken
    /// <paramref name="fromDepartedHost"/>, without those two checks.
    /// </summary>
    string Accept(NetworkShipInfo ship, NetworkShipCondition condition, bool fromDepartedHost);

    /// <summary>
    /// Forgets the reported reserve when the hull's owner departs or its authority moves; a new owner's next check sends its
    /// own reserve.
    /// </summary>
    void ForgetReserve(Guid shipId);

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
    private readonly IMissionContext missionContext;

    private readonly Dictionary<Guid, HullCondition> hulls = new Dictionary<Guid, HullCondition>();

    public BattleShipConditionStream(IBattleNetwork network, IBattleSession session, INavalShipEngine engine,
        IHostEpochPolicy hostEpochPolicy, IMissionContext missionContext)
    {
        this.network = network;
        this.session = session;
        this.engine = engine;
        this.hostEpochPolicy = hostEpochPolicy;
        this.missionContext = missionContext;
    }

    public BattleShipCondition ConditionOf(Guid shipId) => hulls.TryGetValue(shipId, out var hull) ? hull.Condition : null;

    public long RevisionOf(Guid shipId) => hulls.TryGetValue(shipId, out var hull) ? hull.Revision : 0;

    public int? ReportedReserveOf(Guid shipId) => hulls.TryGetValue(shipId, out var hull) ? hull.Reserve : null;

    public void SendIfChanged(NetworkShipInfo ship)
    {
        var hull = Of(ship.ShipId);
        var condition = engine.ReadCondition(ship.Hull);
        if (condition == null) return;

        int reserve = engine.ReadSpawnableReserve(ship.Hull);
        if (!condition.DiffersFrom(hull.Condition) && hull.Reserve == reserve) return;

        hull.Condition = condition;
        hull.Reserve = reserve;
        hull.Revision++;
        hull.Epoch = session.HostEpoch;
        network.SendAll(new NetworkShipCondition(ship.ShipId, session.OwnControllerId, hull.Revision, session.HostEpoch, condition,
            reserve));
    }

    public void SendLastTo(string controllerId, NetworkShipInfo ship)
    {
        if (!hulls.TryGetValue(ship.ShipId, out var hull) || hull.Condition == null || hull.Reserve == null) return;

        network.Send(controllerId, new NetworkShipCondition(ship.ShipId, session.OwnControllerId, hull.Revision, hull.Epoch,
            hull.Condition, hull.Reserve.Value));
    }

    public string Accept(NetworkShipInfo ship, NetworkShipCondition condition, bool fromDepartedHost)
    {
        var hull = Of(ship.ShipId);
        var rejection = ValidateCondition(ship, session.OwnControllerId, condition, hull.Revision, hull.Epoch,
            fromDepartedHost ? null : missionContext.ControllersInMission);
        if (rejection == null && !fromDepartedHost && ship.IsNpcParty && hostEpochPolicy.IsStale(condition.HostEpoch, session.HostEpoch))
            rejection = "stale_epoch";
        if (rejection != null) return rejection;

        // A change of the reserve alone leaves the hull as it is.
        if (condition.Condition.DiffersFrom(hull.Condition)) engine.ApplyCondition(ship.Hull, condition.Condition);
        hull.Condition = condition.Condition;
        hull.Reserve = condition.SpawnableReserve;
        hull.Revision = condition.Revision;
        if (ship.IsNpcParty) hull.Epoch = condition.HostEpoch;
        return null;
    }

    /// <summary>Why a condition must be dropped, or null to apply it.</summary>
    internal static string ValidateCondition(NetworkShipInfo ship, string ownControllerId, NetworkShipCondition condition,
        long acceptedRevision, int acceptedEpoch, IReadOnlyCollection<string> controllersInMission)
    {
        // A new host numbers an AI hull's conditions on from its own count, so a newer epoch restarts the order.
        var rejection = condition.Stamp.Validate(ship.CurrentAuthority, ownControllerId,
            new AuthorityStamp(ship.CurrentAuthority, acceptedEpoch, acceptedRevision), followsHostEpoch: ship.IsNpcParty,
            controllersInMission);
        if (rejection != null) return rejection;
        if (condition.Condition == null || !condition.Condition.IsValid) return "invalid_condition";
        if (condition.SpawnableReserve < 0) return "invalid_reserve";
        return null;
    }

    public void ForgetReserve(Guid shipId)
    {
        if (hulls.TryGetValue(shipId, out var hull)) hull.Reserve = null;
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

    /// <summary>
    /// The owner's last sent, or a copy's last applied, condition and reserve, with their revision and host epoch. Null reserve:
    /// none reported, or forgotten.
    /// </summary>
    private sealed class HullCondition
    {
        public BattleShipCondition Condition;
        public int? Reserve;
        public long Revision;
        public int Epoch;
    }
}
