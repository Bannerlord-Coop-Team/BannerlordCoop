using Common;
using Common.Messaging;
using Missions.Messages;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// [Game thread] The damage state and fieldable reserve of each registered hull. Its owner sends a hull's condition when
/// either changes, and copies take on the owner's in <see cref="AuthorityStamp"/> order, so HP, fire, sinking and the reserve
/// follow the one client that simulates the hull. Conditions follow AI hull authority, so they carry the applied host epoch.
/// </summary>
public interface IBattleShipConditionStream : IDisposable
{
    /// <summary>The owner's last sent, or a copy's last applied, condition of a hull; null before the first.</summary>
    BattleShipCondition ConditionOf(Guid shipId);

    /// <summary>The revision of <see cref="ConditionOf"/>, 0 before the first (diagnostics).</summary>
    long RevisionOf(Guid shipId);

    /// <summary>Why the hull's last dropped condition was dropped, or null (diagnostics).</summary>
    string LastRejectOf(Guid shipId);

    /// <summary>
    /// The fieldable reserve another owner last reported for its hull; none for an own or unregistered hull, once the hull sank,
    /// its owner left or its authority moved.
    /// </summary>
    int ReportedReserveOf(MissionObject hull);

    /// <summary>
    /// A hull's fieldable reserve for the end tally: another owner's hull as last reported, any other hull read from the engine.
    /// </summary>
    int HullReserve(MissionObject hull);

    /// <summary>
    /// [Owner] Sends the hull's condition and fieldable reserve when either differs from the last one sent; the first check
    /// sends once.
    /// </summary>
    void SendIfChanged(NetworkShipInfo ship);

    /// <summary>[Owner] Sends a peer that entered late the hull's last sent condition and reserve, with the same stamp.</summary>
    void SendLastTo(string controllerId, NetworkShipInfo ship);

    /// <summary>
    /// [Mission tick] Applies an owner's condition to its copy. One for a hull not registered yet waits with the hull's held
    /// state, and an AI hull's report from its next host that overtook the host change waits for that change.
    /// </summary>
    void Receive(NetworkShipCondition condition);

    /// <summary>
    /// Applies a departed owner's held condition before its hull moves on; its sender is out of the mission by design, so only
    /// the authority, order and validity checks apply.
    /// </summary>
    void AcceptFromDepartedOwner(NetworkShipInfo ship, NetworkShipCondition condition);

    /// <summary>Forgets the reserves a departed controller reported; the membership gate drops its late reports until it returns.</summary>
    void ForgetReserveOf(string controllerId);

    /// <summary>
    /// The hull's authority moved at an ordered event every client applies: the reserve is forgotten, the order restarts at
    /// the applied host epoch, and a report the new authority sent ahead of the move applies.
    /// </summary>
    void OnAuthorityMoved(NetworkShipInfo ship);

    /// <summary>Drops a hull that left the mission.</summary>
    void Forget(Guid shipId);
}

/// <inheritdoc cref="IBattleShipConditionStream"/>
public class BattleShipConditionStream : IBattleShipConditionStream
{
    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly IMissionContext missionContext;
    private readonly INavalShipEngine engine;
    private readonly IAppliedHostEpoch appliedHost;
    private readonly IHeldShipBuffer heldShips;
    private readonly IBattleShipArrivals arrivals;

    private readonly Dictionary<Guid, HullCondition> hulls = new Dictionary<Guid, HullCondition>();
    // [Game thread] An AI hull's latest condition from a controller that isn't its authority yet: the successor's first report
    // can come over the mesh before the server's host change reaches this client, and only goes out again on change.
    private readonly Dictionary<Guid, NetworkShipCondition> earlyHostConditions = new Dictionary<Guid, NetworkShipCondition>();

    // Subscribes to the hull conditions of this battle's mesh.
    public BattleShipConditionStream(IBattleNetwork network, IMessageBroker messageBroker, BattleContext context,
        INavalShipEngine engine, IAppliedHostEpoch appliedHost, IHeldShipBuffer heldShips, IBattleShipArrivals arrivals)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        session = context.Session;
        missionComponent = context.MissionComponent;
        missionContext = context.MissionContext;
        this.engine = engine;
        this.appliedHost = appliedHost;
        this.heldShips = heldShips;
        this.arrivals = arrivals;

        messageBroker.Subscribe<NetworkShipCondition>(Handle_NetworkShipCondition);
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    // Drops the subscription.
    public void Dispose() => messageBroker.Unsubscribe<NetworkShipCondition>(Handle_NetworkShipCondition);

    public BattleShipCondition ConditionOf(Guid shipId) => hulls.TryGetValue(shipId, out var hull) ? hull.Condition : null;

    public long RevisionOf(Guid shipId) => hulls.TryGetValue(shipId, out var hull) ? hull.Revision : 0;

    public string LastRejectOf(Guid shipId) => hulls.TryGetValue(shipId, out var hull) ? hull.LastReject : null;

    // The owner reports it with the hull's condition, on change, so no frame length expires it: vanilla latches a depleted
    // side the first frame it reads one.
    public int ReportedReserveOf(MissionObject hull)
    {
        if (!Registry.TryGetByHull(hull, out var ship) || IsOwnHull(ship)) return 0;
        if (!hulls.TryGetValue(ship.ShipId, out var condition) || condition.Condition?.SinkingState == BattleShipCondition.Sunk)
            return 0;

        return condition.Reserve ?? 0;
    }

    // A copy reads 0 from the engine and an own hull 0 from the reports, so a hull mid-swap counts 0 either way.
    public int HullReserve(MissionObject hull) =>
        Registry.TryGetByHull(hull, out var ship) && !IsOwnHull(ship) ? ReportedReserveOf(hull) : engine.ReadSpawnableReserve(hull);

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
        hull.Epoch = appliedHost.AppliedHostEpoch;
        network.SendAll(new NetworkShipCondition(ship.ShipId, session.OwnControllerId, hull.Revision, hull.Epoch, condition, reserve));
    }

    public void SendLastTo(string controllerId, NetworkShipInfo ship)
    {
        if (!hulls.TryGetValue(ship.ShipId, out var hull) || hull.Condition == null || hull.Reserve == null) return;

        network.Send(controllerId, new NetworkShipCondition(ship.ShipId, session.OwnControllerId, hull.Revision, hull.Epoch,
            hull.Condition, hull.Reserve.Value));
    }

    // Sent once on change, so it waits behind the hull traffic that arrived first and applies from the mission tick.
    private void Handle_NetworkShipCondition(MessagePayload<NetworkShipCondition> payload)
    {
        var condition = payload.What;
        GameThread.RunSafe(() => arrivals.Enqueue(() => Receive(condition), nameof(Handle_NetworkShipCondition)),
            context: nameof(Handle_NetworkShipCondition));
    }

    public void Receive(NetworkShipCondition condition)
    {
        if (!Registry.TryGet(condition.ShipId, out var ship))
        {
            if (Registry.StateOf(condition.ShipId) == ShipLifecycle.Removed) return;

            heldShips.Of(condition.ShipId).HoldCondition(condition);
            return;
        }

        var rejection = Accept(ship, condition, missionContext.ControllersInMission);
        if (rejection == null) return;

        Of(ship.ShipId).LastReject = rejection;
        if (IsEarlyHostCondition(ship.FollowsBattleHost, IsFromAnotherAuthority(ship, condition), condition.HostEpoch,
            appliedHost.AppliedHostEpoch))
            HoldEarlyHostCondition(condition);
    }

    public void AcceptFromDepartedOwner(NetworkShipInfo ship, NetworkShipCondition condition) => Accept(ship, condition, null);

    // Applies a condition that passes the order and validity checks; why it was dropped, or null.
    private string Accept(NetworkShipInfo ship, NetworkShipCondition condition, IReadOnlyCollection<string> controllersInMission)
    {
        var hull = Of(ship.ShipId);
        var rejection = ValidateCondition(ship, session.OwnControllerId, condition, hull.Revision, hull.Epoch, controllersInMission);
        if (rejection != null) return rejection;

        // A change of the reserve alone leaves the hull as it is.
        if (condition.Condition.DiffersFrom(hull.Condition)) engine.ApplyCondition(ship.Hull, condition.Condition);
        hull.Condition = condition.Condition;
        hull.Reserve = condition.SpawnableReserve;
        hull.Revision = condition.Revision;
        if (ship.FollowsBattleHost) hull.Epoch = condition.HostEpoch;
        return null;
    }

    /// <summary>Why a condition must be dropped, or null to apply it.</summary>
    internal static string ValidateCondition(NetworkShipInfo ship, string ownControllerId, NetworkShipCondition condition,
        long acceptedRevision, int acceptedEpoch, IReadOnlyCollection<string> controllersInMission)
    {
        // A new host numbers an AI hull's conditions on from its own count, so a newer epoch restarts the order.
        var rejection = condition.Stamp.Validate(ship.CurrentAuthority, ownControllerId,
            new AuthorityStamp(ship.CurrentAuthority, acceptedEpoch, acceptedRevision), followsHostEpoch: ship.FollowsBattleHost,
            controllersInMission);
        if (rejection != null) return rejection;
        if (condition.Condition == null || !condition.Condition.IsValid) return "invalid_condition";
        if (condition.SpawnableReserve < 0) return "invalid_reserve";
        return null;
    }

    /// <summary>
    /// Whether a dropped condition is an AI hull's from a controller other than its authority, at a newer host epoch than this
    /// client applied: the successor's report that overtook the host change, kept until that change moves the hull to it.
    /// </summary>
    internal static bool IsEarlyHostCondition(bool followsHost, bool fromAnotherAuthority, int conditionEpoch, int appliedEpoch) =>
        followsHost && fromAnotherAuthority && conditionEpoch > appliedEpoch;

    // A peer's report for a hull another peer simulates; this client's own hulls never take one.
    private bool IsFromAnotherAuthority(NetworkShipInfo ship, NetworkShipCondition condition) =>
        !IsOwnHull(ship) && condition.OwnerControllerId != ship.CurrentAuthority;

    // Keeps the newest early report per hull.
    private void HoldEarlyHostCondition(NetworkShipCondition condition)
    {
        if (earlyHostConditions.TryGetValue(condition.ShipId, out var held) && !condition.Stamp.IsNewer(held.Stamp)) return;

        earlyHostConditions[condition.ShipId] = condition;
    }

    public void ForgetReserveOf(string controllerId)
    {
        foreach (var ship in Registry.Ships.Where(ship => ship.CurrentAuthority == controllerId))
        {
            if (hulls.TryGetValue(ship.ShipId, out var hull)) hull.Reserve = null;
        }
    }

    // The new authority numbers its conditions from its own count at the applied epoch, which the host change set first.
    public void OnAuthorityMoved(NetworkShipInfo ship)
    {
        if (hulls.TryGetValue(ship.ShipId, out var hull))
        {
            hull.Reserve = null;
            hull.Revision = 0;
            hull.Epoch = appliedHost.AppliedHostEpoch;
        }

        if (!earlyHostConditions.TryGetValue(ship.ShipId, out var early)) return;

        earlyHostConditions.Remove(ship.ShipId);
        if (early.OwnerControllerId == ship.CurrentAuthority) Receive(early);
    }

    public void Forget(Guid shipId)
    {
        hulls.Remove(shipId);
        earlyHostConditions.Remove(shipId);
    }

    // A hull this client simulates.
    private bool IsOwnHull(NetworkShipInfo ship) => ship.CurrentAuthority == session.OwnControllerId;

    // The hull's condition state, created on first use.
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
    /// The owner's last sent, or a copy's last applied, condition and reserve, with their revision and host epoch, and why the
    /// last one was dropped. Null reserve: none reported, or forgotten.
    /// </summary>
    private sealed class HullCondition
    {
        public BattleShipCondition Condition;
        public int? Reserve;
        public long Revision;
        public int Epoch;
        public string LastReject;
    }
}
