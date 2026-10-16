using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Replicates naval hulls over the mission mesh. Own hulls register when they spawn, are announced to peers at
/// the first deployment commit and stream their frame, helm input and ropes at 20 Hz; other owners' hulls are
/// spawned as kinematic copies once this client's deployment is over and follow those samples. Ropes ride the samples
/// because each one carries every station's generation-ordered state, so a later sample repairs a rejected one; leaving
/// sends them once more. Owners also send a hull's damage state when it changes, and copies take it on.
/// </summary>
public interface IBattleShipReplicator : IDisposable
{
    /// <summary>[Game thread] Announce, stream and interpolate hulls for this frame.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] Registry and stream state per hull (diagnostics).</summary>
    object Inspect();

    /// <summary>
    /// [Game thread, host] Registers an AI (NPC) party's hull the host spawned, owned by the host, and announces it
    /// so peers spawn a copy on that party's side.
    /// </summary>
    Guid RegisterNpcHull(MissionObject hull, Formation formation, string mapEventPartyId);

    /// <summary>
    /// [Game thread] Queues replacing an AI hull this client simulates with a fresh simulated hull of the same ship, one
    /// step per mission tick, keeping its ship id. Why it cannot, or null.
    /// </summary>
    string ReplaceNpcHull(Guid shipId);

    /// <summary>[Game thread] Rope state per throw station of every registered hull (diagnostics).</summary>
    object InspectRopes();

    /// <summary>
    /// [Game thread] The fieldable reserve another owner last reported for its hull; none once the hull sank, its owner left
    /// or its authority moved.
    /// </summary>
    int ReportedReserveOf(MissionObject hull);

    /// <summary>
    /// [Game thread] A hull's fieldable reserve for the end tally: another owner's hull as last reported, any other hull
    /// read from the engine.
    /// </summary>
    int HullReserve(MissionObject hull);

    /// <summary>[Game thread] Whether an AI hull this client took over is still being replaced by a simulated one.</summary>
    bool IsReplacingHull(Guid shipId);

    /// <summary>
    /// [Game thread] Sends the own hulls' rope state once more as the mission ends, while the hulls are still live; samples
    /// end with the mission, so peers would otherwise keep the last sample.
    /// </summary>
    void SendFinalRopes();
}

/// <inheritdoc cref="IBattleShipReplicator"/>
public class BattleShipReplicator : IBattleShipReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipReplicator>();

    internal const float SendIntervalSeconds = 0.05f;
    internal const float InterpolationSeconds = 0.05f;
    // Measured on this client's mission clock from receipt, since peers' wall clocks are not in step.
    internal const float SampleLifetimeSeconds = 1f;
    // A foreign hull that cannot spawn (no free formation or ships logic yet) is retried this often before it is dropped.
    internal const int ForeignHullSpawnAttempts = 5;
    internal const float ForeignHullRetrySeconds = 1f;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly ICoopMissionComponent missionComponent;
    private readonly IHostEpochPolicy hostEpochPolicy;
    private readonly IBattleResultCommitter resultCommitter;
    private readonly INavalShipEngine engine;
    private readonly IBattleTeamResolver teamResolver;
    private readonly IObjectManager objectManager;
    private readonly INavalHullAuthority hullAuthority;
    private readonly IBattleShipConditionStream conditions;

    private readonly Dictionary<Guid, ShipStream> streams = new Dictionary<Guid, ShipStream>();
    // Everything received for a ship that is not registered here yet, applied in one ordered drain once it is.
    private readonly Dictionary<Guid, HeldShip> heldShips = new Dictionary<Guid, HeldShip>();
    private long recordsHeld;
    // Departures come over the campaign connection and samples over the mission mesh, so a departed owner's late sample is not
    // ordered behind its departure; its reserve stays dropped until the controller enters again.
    private readonly HashSet<string> departedControllers = new HashSet<string>();
    // The host AI hulls last migrated to, so an AI copy whose record was still waiting registers under it too.
    private string npcHullHost;
    private bool spawnRecordsSent;
    private float sendElapsed;
    private float elapsed;
    private long finalRopesSent, finalRopesAccepted, finalRopesRejected;

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    internal Guid ShipIdOf(MissionObject hull) => Registry.TryGetByHull(hull, out var ship) ? ship.ShipId : Guid.Empty;

    internal MissionObject HullOf(Guid shipId) => Registry.TryGet(shipId, out var ship) ? ship.Hull : null;

    public BattleShipReplicator(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        BattleContext context,
        INavalShipEngine engine,
        IBattleTeamResolver teamResolver,
        IObjectManager objectManager,
        INavalHullAuthority hullAuthority,
        IBattleShipConditionStream conditions)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        session = context.Session;
        deployment = context.Deployment;
        missionComponent = context.MissionComponent;
        hostEpochPolicy = context.HostEpochPolicy;
        resultCommitter = context.ResultCommitter;
        this.engine = engine;
        this.teamResolver = teamResolver;
        this.objectManager = objectManager;
        this.hullAuthority = hullAuthority;
        this.conditions = conditions;

        messageBroker.Subscribe<ShipSpawnedInBattle>(Handle_ShipSpawned);
        messageBroker.Subscribe<NetworkSpawnBattleShips>(Handle_NetworkSpawnBattleShips);
        messageBroker.Subscribe<NetworkBattleShipSample>(Handle_NetworkBattleShipSample);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Subscribe<NetworkBattleRopeFinal>(Handle_NetworkBattleRopeFinal);
        messageBroker.Subscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
        messageBroker.Subscribe<NetworkShipCondition>(Handle_NetworkShipCondition);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ShipSpawnedInBattle>(Handle_ShipSpawned);
        messageBroker.Unsubscribe<NetworkSpawnBattleShips>(Handle_NetworkSpawnBattleShips);
        messageBroker.Unsubscribe<NetworkBattleShipSample>(Handle_NetworkBattleShipSample);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<NetworkBattleRopeFinal>(Handle_NetworkBattleRopeFinal);
        messageBroker.Unsubscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
        messageBroker.Unsubscribe<NetworkShipCondition>(Handle_NetworkShipCondition);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);
        heldShips.Clear();
        departedControllers.Clear();
    }

    // [Game thread] Published synchronously by the spawn postfix. Hulls this client fields go on PlayerTeam; the
    // copies and replacement hulls this replicator spawns raise the same event and are registered by their spawn path instead.
    private void Handle_ShipSpawned(MessagePayload<ShipSpawnedInBattle> payload)
    {
        var hull = payload.What.Hull;
        var formation = payload.What.Formation;
        if (hullAuthority.IsSpawnRegistrationSuppressed || hull == null || formation?.Team == null
            || formation.Team != Mission.Current?.PlayerTeam)
            return;

        var ship = new NetworkShipInfo(Guid.NewGuid(), session.OwnControllerId, GetOwnMapEventPartyId(), false, hull, formation);
        if (Registry.TryRegister(ship))
            Logger.Information("[NavalSync] Registered own hull {ShipId} on formation {Formation} at deployment position {Position}",
                ship.ShipId, formation.FormationIndex, engine.GetFrame(hull).origin);
    }

    public Guid RegisterNpcHull(MissionObject hull, Formation formation, string mapEventPartyId)
    {
        var ship = new NetworkShipInfo(Guid.NewGuid(), session.OwnControllerId, mapEventPartyId, true, hull, formation);
        if (!Registry.TryRegister(ship)) return Guid.Empty;

        // AI is never withheld behind the deployment commit, so peers see these hulls as soon as they exist.
        network.SendAll(new NetworkSpawnBattleShips(new[] { engine.Describe(hull, ship) }));
        Logger.Information("[NavalSync] Registered AI hull {ShipId} of party {PartyId} on formation {Formation} at {Position}",
            ship.ShipId, mapEventPartyId, formation?.FormationIndex, engine.GetFrame(hull).origin);
        return ship.ShipId;
    }

    // AI hulls follow the battle host. Every client moves their authority; the new host takes over simulating
    // them and the old host, if it is still here, hands them back to the samples.
    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        var migrated = payload.What;
        if (migrated.MapEventId != session.InstanceId) return;
        if (!BattleAuthorityMigrator.AppliesHostMigration(resultCommitter, session.IsNaval)) return;

        GameThread.RunSafe(() => TransferNpcHulls(migrated.NewHostControllerId ?? session.OwnControllerId),
            context: nameof(Handle_BattleHostMigrated));
    }

    /// <summary>[Game thread] Moves every AI hull to <paramref name="newHost"/> and flips local simulation where ours changed.</summary>
    internal int TransferNpcHulls(string newHost)
    {
        npcHullHost = newHost;
        int transferred = 0;
        foreach (var ship in Registry.Ships)
        {
            if (!ship.IsNpcParty || ship.CurrentAuthority == newHost) continue;

            TransferNpcHull(ship, newHost);
            transferred++;
        }

        return transferred;
    }

    private void TransferNpcHull(NetworkShipInfo ship, string newHost)
    {
        bool isOwn = hullAuthority.Transfer(ship, newHost);

        // The old owner's interpolation target must not keep writing frames to a hull this client now simulates.
        var stream = GetStream(ship.ShipId);
        stream.Target = null;
        stream.ForgetReportedReserve();
        Logger.Information("[NavalSync] AI hull {ShipId} moved to host {Host}{Local}", ship.ShipId, newHost, isOwn ? " (this client)" : "");
    }

    public string ReplaceNpcHull(Guid shipId)
    {
        if (!Registry.TryGet(shipId, out var ship)) return "No registered hull has that ship id.";
        if (!ship.IsNpcParty || !IsOwnHull(ship)) return "The hull is not an AI hull this client simulates.";
        return hullAuthority.Replace(shipId);
    }

    public bool IsReplacingHull(Guid shipId) => hullAuthority.IsReplacing(shipId);

    public void Tick(float dt)
    {
        elapsed += Math.Max(0f, dt);
        hullAuthority.Tick();
        PruneRemovedHulls();
        DrainHeldShips();

        if (!spawnRecordsSent && deployment.IsCommitted)
        {
            spawnRecordsSent = true;
            SendSpawnRecords(null);
        }

        if (spawnRecordsSent)
            SendOwnSamples(dt);

        ApplyForeignFrames(dt);
    }

    private void PruneRemovedHulls()
    {
        var live = engine.Hulls;
        foreach (var ship in Registry.Ships)
        {
            if (live.Contains(ship.Hull)) continue;

            Registry.Remove(ship.ShipId);
            streams.Remove(ship.ShipId);
            conditions.Forget(ship.ShipId);
            Logger.Information("[NavalSync] Hull {ShipId} of {Owner} left the mission (deployment mode {DeploymentMode}); dropped it",
                ship.ShipId, ship.CurrentAuthority, engine.IsDeploymentMode);
        }
    }

    // [Game thread] Own hulls only; a joiner that enters after the commit gets the same records directly.
    private void SendSpawnRecords(string controllerId)
    {
        var records = Registry.Ships
            .Where(IsOwnHull)
            .Select(ship => engine.Describe(ship.Hull, ship))
            .ToArray();
        if (records.Length == 0) return;

        var message = new NetworkSpawnBattleShips(records);
        if (controllerId == null) network.SendAll(message);
        else network.Send(controllerId, message);

        Logger.Information("[NavalSync] Sent {Count} own hull record(s) to {Target}", records.Length, controllerId ?? "all peers");
    }

    private void SendOwnSamples(float dt)
    {
        sendElapsed += dt;
        if (sendElapsed < SendIntervalSeconds) return;
        sendElapsed = 0;

        foreach (var ship in Registry.Ships.Where(IsOwnHull))
        {
            var stream = GetStream(ship.ShipId);
            // The sunk condition already went out after the hull's last sample; peers keep that frame.
            if (conditions.ConditionOf(ship.ShipId)?.SinkingState == BattleShipCondition.Sunk) continue;

            stream.Sent++;
            network.SendAll(new NetworkBattleShipSample(ship.ShipId, session.OwnControllerId, stream.Sent,
                NetworkBattleShipSample.FromFrame(engine.GetFrame(ship.Hull)), engine.ReadInput(ship.Hull),
                engine.CaptureRopes(ship.Hull, ShipIdOf), session.HostEpoch, engine.ReadSpawnableReserve(ship.Hull)));
            conditions.SendIfChanged(ship);
        }
    }

    private void Handle_NetworkShipCondition(MessagePayload<NetworkShipCondition> payload)
    {
        var condition = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            AcceptCondition(condition);
        }, context: nameof(Handle_NetworkShipCondition));
    }

    /// <summary>[Game thread] Sets an owner's condition on its copied hull; one for a hull not spawned here yet waits for it.</summary>
    internal void AcceptCondition(NetworkShipCondition condition)
    {
        if (condition == null) return;

        if (!Registry.TryGet(condition.ShipId, out var ship))
        {
            var held = Held(condition.ShipId);
            if (held.Condition == null || condition.Stamp.IsNewer(held.Condition.Stamp)) held.Condition = condition;
            return;
        }

        var stream = GetStream(condition.ShipId);
        var rejection = conditions.Accept(ship, condition, checkHostEpoch: true);
        if (rejection != null) stream.Reject("condition_" + rejection);
    }

    public void SendFinalRopes()
    {
        if (!spawnRecordsSent) return;

        foreach (var ship in Registry.Ships.Where(IsOwnHull))
        {
            var ropes = engine.CaptureRopes(ship.Hull, ShipIdOf);
            if (ropes == null || ropes.Length == 0) continue;

            network.SendAll(new NetworkBattleRopeFinal(ship.ShipId, session.OwnControllerId, ropes));
            finalRopesSent++;
        }
    }

    private void Handle_NetworkBattleRopeFinal(MessagePayload<NetworkBattleRopeFinal> payload)
    {
        var final = payload.What;
        GameThread.RunSafe(() => AcceptFinalRopes(final), context: nameof(Handle_NetworkBattleRopeFinal));
    }

    private void AcceptFinalRopes(NetworkBattleRopeFinal final)
    {
        if (Mission.Current == null || final == null) return;

        if (!Registry.TryGet(final.ShipId, out var ship))
        {
            if (Registry.StateOf(final.ShipId) == ShipLifecycle.Awaiting) Held(final.ShipId).FinalRopes = final;
            else finalRopesRejected++;
            return;
        }

        if (ValidateFinalRopes(ship, session.OwnControllerId, final) != null)
        {
            finalRopesRejected++;
            return;
        }

        engine.ApplyRopes(ship.Hull, final.Ropes, HullOf, final: true);
        finalRopesAccepted++;
    }

    /// <summary>Why a final rope state must be dropped, or null to apply it.</summary>
    internal static string ValidateFinalRopes(NetworkShipInfo ship, string ownControllerId, NetworkBattleRopeFinal final)
    {
        var rejection = AuthorityStamp.ValidateSender(final.OwnerControllerId, ship.CurrentAuthority, ownControllerId);
        if (rejection != null) return rejection;
        if (final.Ropes == null || !BattleRopeState.AreValid(final.Ropes)) return "invalid_ropes";
        return null;
    }

    private void Handle_NetworkSpawnBattleShips(MessagePayload<NetworkSpawnBattleShips> payload)
    {
        var records = payload.What.Ships ?? Array.Empty<BattleShipSpawnData>();
        GameThread.RunSafe(() => AcceptSpawnRecords(records), context: nameof(Handle_NetworkSpawnBattleShips));
    }

    /// <summary>[Game thread] Queues other owners' hull records and spawns the copies that can spawn now.</summary>
    internal void AcceptSpawnRecords(IEnumerable<BattleShipSpawnData> records)
    {
        foreach (var record in records.Where(record => record != null))
        {
            // A repeated record (the host announces its AI hulls again at its commit) keeps the first one's place.
            var held = Held(record.ShipId);
            if (held.Record != null) continue;

            held.Record = record;
            held.RecordOrder = ++recordsHeld;
        }

        DrainHeldRecords();
    }

    private HeldShip Held(Guid shipId)
    {
        if (!heldShips.TryGetValue(shipId, out var held))
        {
            held = new HeldShip();
            heldShips.Add(shipId, held);
        }

        return held;
    }

    // Records spawn first, in arrival order; then a condition held for a hull that registered without a record goes on.
    private void DrainHeldShips()
    {
        if (heldShips.Count == 0) return;

        DrainHeldRecords();
        foreach (var entry in heldShips.Where(entry => entry.Value.Record == null).ToArray())
        {
            if (!Registry.TryGet(entry.Key, out _)) continue;

            heldShips.Remove(entry.Key);
            if (entry.Value.Condition != null) AcceptCondition(entry.Value.Condition);
        }
    }

    private void DrainHeldRecords()
    {
        foreach (var held in heldShips.Values.Where(held => held.Record != null).OrderBy(held => held.RecordOrder).ToArray())
        {
            if (!TrySpawnForeignHull(held)) Registry.MarkAwaiting(held.Record.ShipId);
        }
    }

    // [Game thread] True when the record is settled (spawned or dropped), false to retry next tick.
    private bool TrySpawnForeignHull(HeldShip held)
    {
        var data = held.Record;
        if (Mission.Current == null || session.IsOwn(data.OwnerControllerId) || Registry.TryGet(data.ShipId, out _))
        {
            held.SettleRecord();
            return true;
        }
        // Vanilla's team setup spawns a hull for every set ship assignment, so a copy spawned before it would get a local twin.
        if (!deployment.IsTeamSetupOver) return false;

        // Vanilla's deployment finish runs over every hull and pools removed ones, so a copy spawns once it is over.
        if (engine.IsDeploymentMode)
        {
            if (!held.HeldForDeployment)
            {
                held.HeldForDeployment = true;
                Logger.Information("[NavalSync] Holding foreign hull {ShipId} ({Hull}) of {Owner} until local deployment ends",
                    data.ShipId, data.HullId, data.OwnerControllerId);
            }
            return false;
        }

        var team = teamResolver.ResolveReplicatedTeam(data.Side, isOwn: false);
        if (team == null) return false;
        if (held.FailedAttempts > 0 && elapsed < held.RetryAt) return false;

        MissionObject hull;
        Formation formation;
        using (hullAuthority.SuppressSpawnRegistration())
        {
            hull = engine.SpawnForeignHull(data, team, out formation);
        }

        if (hull == null) return GiveUpForeignHullSpawn(held, team);

        heldShips.Remove(data.ShipId);
        var ship = new NetworkShipInfo(data.ShipId, data.OwnerControllerId, data.MapEventPartyId, data.IsNpcParty, hull, formation);
        bool registered = Registry.TryRegister(ship);
        Logger.Information("[NavalSync] Spawned foreign hull {ShipId} ({Hull}) of {Owner} on team {Team} formation {Formation}{Held}",
            data.ShipId, data.HullId, data.OwnerControllerId, team.TeamSide, formation?.FormationIndex,
            held.HeldForDeployment ? " after local deployment" : "");
        // A host migration while the record waited only moved the registered AI hulls.
        if (registered && ship.IsNpcParty && npcHullHost != null && ship.CurrentAuthority != npcHullHost)
        {
            ApplyDepartedHostState(ship, held);
            TransferNpcHull(ship, npcHullHost);
        }

        ApplyHeldState(held);
        return true;
    }

    // The departed host's last held state goes on before the move, so the new host takes over from its pose and damage.
    // Its epoch is behind by now, so only the authority, order and validity checks apply.
    private void ApplyDepartedHostState(NetworkShipInfo ship, HeldShip held)
    {
        var departedHost = ship.CurrentAuthority;
        var stream = GetStream(ship.ShipId);
        var condition = held.Condition;
        if (condition?.OwnerControllerId == departedHost)
        {
            held.Condition = null;
            conditions.Accept(ship, condition, checkHostEpoch: false);
        }

        var sample = held.Sample;
        if (sample?.OwnerControllerId == departedHost)
        {
            held.Sample = null;
            if (ValidateSample(ship, session.OwnControllerId, sample, stream.Accepted, stream.AcceptedEpoch) == null)
            {
                TakeSample(ship, stream, sample);
                // The move clears the interpolation target, so the frame is written now.
                var frame = NetworkBattleShipSample.ToFrame(sample.Frame);
                engine.ApplyForeignFrame(ship.Hull, frame);
                stream.Written = frame;
                stream.HasWritten = true;
                stream.Applied = sample.Sequence;
            }
        }

        var final = held.FinalRopes;
        if (final?.OwnerControllerId == departedHost)
        {
            held.FinalRopes = null;
            AcceptFinalRopes(final);
        }
    }

    // The owner's latest state received while the copy waited, so it does not sit at the record's frame until the next sample.
    private void ApplyHeldState(HeldShip held)
    {
        if (held.Condition != null) AcceptCondition(held.Condition);
        if (held.Sample != null) AcceptSample(held.Sample);
        if (held.FinalRopes != null) AcceptFinalRopes(held.FinalRopes);
    }

    // [Game thread] Counts a failed spawn; true once the hull is dropped, which also drops the crew buffered for it.
    private bool GiveUpForeignHullSpawn(HeldShip held, Team team)
    {
        var data = held.Record;
        int attempts = held.FailedAttempts + 1;
        if (!IsForeignHullSpawnExhausted(attempts))
        {
            held.FailedAttempts = attempts;
            held.RetryAt = elapsed + ForeignHullRetrySeconds;
            return false;
        }

        // A condition held for the hull stays, as before, for a later record of the same ship.
        held.SettleRecord();
        held.Sample = null;
        held.FinalRopes = null;
        Registry.MarkSpawnFailed(data.ShipId);
        Logger.Error("[NavalSync] Could not spawn hull {ShipId} ({Hull}) of {Owner} on team {Team} after {Attempts} attempts; dropping it and its crew",
            data.ShipId, data.HullId, data.OwnerControllerId, team.TeamSide, attempts);
        return true;
    }

    internal static bool IsForeignHullSpawnExhausted(int attempts) => attempts >= ForeignHullSpawnAttempts;

    private void Handle_NetworkBattleShipSample(MessagePayload<NetworkBattleShipSample> payload)
    {
        var sample = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            AcceptSample(sample);
        }, context: nameof(Handle_NetworkBattleShipSample));
    }

    /// <summary>[Game thread] Takes an owner's sample as the copied hull's next interpolation target.</summary>
    internal void AcceptSample(NetworkBattleShipSample sample)
    {
        if (sample == null) return;

        if (!Registry.TryGet(sample.ShipId, out var ship))
        {
            if (Registry.StateOf(sample.ShipId) == ShipLifecycle.Awaiting) HoldSample(sample);
            else GetStream(sample.ShipId).Reject("unknown_ship");
            return;
        }

        var stream = GetStream(sample.ShipId);

        var rejection = ValidateSample(ship, session.OwnControllerId, sample, stream.Accepted, stream.AcceptedEpoch);
        // BR-102: an AI hull follows the host, so a superseded hosting generation is dropped like siege state.
        if (rejection == null && ship.IsNpcParty && hostEpochPolicy.IsStale(sample.HostEpoch, session.HostEpoch))
            rejection = "stale_epoch";
        if (rejection != null)
        {
            stream.Reject(rejection);
            return;
        }

        TakeSample(ship, stream, sample);
    }

    private void TakeSample(NetworkShipInfo ship, ShipStream stream, NetworkBattleShipSample sample)
    {
        if (ship.IsNpcParty) stream.AcceptedEpoch = sample.HostEpoch;

        engine.ApplyInput(ship.Hull, sample.Input);
        engine.ApplyRopes(ship.Hull, sample.Ropes, HullOf, final: false);
        stream.Start = stream.HasWritten ? stream.Written : engine.GetFrame(ship.Hull);
        stream.Target = sample;
        stream.TargetReceivedAt = elapsed;
        stream.Accepted = sample.Sequence;
        stream.Elapsed = 0;
        if (departedControllers.Contains(sample.OwnerControllerId)) return;

        stream.ReportedReserve = sample.SpawnableReserve;
        stream.HasReportedReserve = true;
    }

    // Kept past the sample lifetime: vanilla latches a depleted side the first frame it reads one, so one long frame must not
    // end the battle. Sinking, the owner leaving or an authority move drop it instead.
    public int ReportedReserveOf(MissionObject hull)
    {
        if (!Registry.TryGetByHull(hull, out var ship) || IsOwnHull(ship) || !streams.TryGetValue(ship.ShipId, out var stream))
            return 0;
        if (!stream.HasReportedReserve || conditions.ConditionOf(ship.ShipId)?.SinkingState == BattleShipCondition.Sunk) return 0;

        return stream.ReportedReserve;
    }

    // A copy reads 0 from the engine and an own hull 0 from the reports, so a hull mid-swap counts 0 either way.
    public int HullReserve(MissionObject hull) =>
        Registry.TryGetByHull(hull, out var ship) && !IsOwnHull(ship) ? ReportedReserveOf(hull) : engine.ReadSpawnableReserve(hull);

    private void Handle_MissionPeerLeft(MessagePayload<MissionPeerLeft> payload) =>
        ForgetReportsOfDeparted(payload.What.ControllerId, payload.What.InstanceId);

    private void Handle_MissionPeerDisconnected(MessagePayload<MissionPeerDisconnected> payload) =>
        ForgetReportsOfDeparted(payload.What.ControllerId, payload.What.InstanceId);

    private void ForgetReportsOfDeparted(string controllerId, string instanceId)
    {
        if (string.IsNullOrEmpty(controllerId) || (instanceId != null && instanceId != session.InstanceId)) return;

        GameThread.RunSafe(() => ForgetReportedReserveOf(controllerId), context: nameof(ForgetReportsOfDeparted));
    }

    /// <summary>[Game thread] Drops the reserve a departed controller reported for the hulls it held, and ignores its late reports.</summary>
    internal void ForgetReportedReserveOf(string controllerId)
    {
        departedControllers.Add(controllerId);
        foreach (var ship in Registry.Ships)
        {
            if (ship.CurrentAuthority == controllerId && streams.TryGetValue(ship.ShipId, out var stream))
                stream.ForgetReportedReserve();
        }
    }

    private void HoldSample(NetworkBattleShipSample sample)
    {
        var held = Held(sample.ShipId);
        if (held.Sample == null || sample.Stamp.IsNewer(held.Sample.Stamp)) held.Sample = sample;
    }

    /// <summary>Why a sample must be dropped, or null to accept it.</summary>
    internal static string ValidateSample(NetworkShipInfo ship, string ownControllerId, NetworkBattleShipSample sample,
        long acceptedSequence, int acceptedEpoch)
    {
        // A new host numbers an AI hull's samples from its own stream, so a newer epoch restarts the sequence.
        var rejection = sample.Stamp.Validate(ship.CurrentAuthority, ownControllerId,
            new AuthorityStamp(ship.CurrentAuthority, acceptedEpoch, acceptedSequence), followsHostEpoch: ship.IsNpcParty);
        if (rejection != null) return rejection;
        if (!sample.HasValidFrame) return "invalid_frame";
        if (!sample.Input.IsValid) return "invalid_input";
        if (sample.SpawnableReserve < 0) return "invalid_reserve";
        if (!BattleRopeState.AreValid(sample.Ropes)) return "invalid_ropes";
        return null;
    }

    private void ApplyForeignFrames(float dt)
    {
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;

        foreach (var entry in streams)
        {
            var stream = entry.Value;
            var target = stream.Target;
            if (target == null || !Registry.TryGet(entry.Key, out var ship) || IsOwnHull(ship)) continue;

            if (elapsed - stream.TargetReceivedAt > SampleLifetimeSeconds)
            {
                stream.Target = null;
                stream.Reject("expired");
                continue;
            }

            if (stream.Elapsed >= InterpolationSeconds) continue;

            stream.Elapsed = Math.Min(InterpolationSeconds, stream.Elapsed + dt);
            var frame = MatrixFrame.Lerp(stream.Start, NetworkBattleShipSample.ToFrame(target.Frame), stream.Elapsed / InterpolationSeconds);
            engine.ApplyForeignFrame(ship.Hull, frame);
            stream.Written = frame;
            stream.HasWritten = true;
            stream.Applied = target.Sequence;
        }
    }

    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var entered = payload.What;
        if (entered.InstanceId != null && entered.InstanceId != session.InstanceId) return;

        GameThread.RunSafe(() =>
        {
            departedControllers.Remove(entered.ControllerId);
            if (spawnRecordsSent && !session.IsOwn(entered.ControllerId))
                SendSpawnRecords(entered.ControllerId);
        }, context: nameof(Handle_PeerEntered));
    }

    private bool IsOwnHull(NetworkShipInfo ship) => ship.CurrentAuthority == session.OwnControllerId;

    private string GetOwnMapEventPartyId()
    {
        var mapEventParty = MobileParty.MainParty?.MapEvent?.FindMapEventParty(PartyBase.MainParty);
        return mapEventParty != null && objectManager.TryGetId(mapEventParty, out var id) ? id : null;
    }

    private ShipStream GetStream(Guid shipId)
    {
        if (!streams.TryGetValue(shipId, out var stream))
        {
            stream = new ShipStream();
            streams.Add(shipId, stream);
        }

        return stream;
    }

    public object Inspect() => new
    {
        spawnRecordsSent,
        pendingForeignHulls = heldShips.Values.Count(held => held.Record != null),
        heldForDeployment = heldShips.Values.Count(held => held.HeldForDeployment),
        heldSamples = heldShips.Values.Count(held => held.Sample != null),
        ships = Registry.Ships.Select(ship =>
        {
            streams.TryGetValue(ship.ShipId, out var stream);
            var condition = engine.ReadCondition(ship.Hull);
            return new
            {
                shipId = ship.ShipId,
                role = IsOwnHull(ship) ? "owner" : "foreign",
                currentAuthority = ship.CurrentAuthority,
                originalOwner = ship.OriginalOwner,
                mapEventPartyId = ship.MapEventPartyId,
                isNpcParty = ship.IsNpcParty,
                formation = ship.Formation?.FormationIndex.ToString(),
                team = ship.Formation?.Team?.TeamSide.ToString(),
                bodyActive = engine.IsBodyActive(ship.Hull),
                controller = engine.ControllerName(ship.Hull),
                input = engine.ReadInput(ship.Hull),
                sent = stream?.Sent ?? 0,
                accepted = stream?.Accepted ?? 0,
                applied = stream?.Applied ?? 0,
                rejects = stream?.Rejects ?? 0,
                lastReject = stream?.LastReject,
                streaming = stream?.Target != null,
                epoch = IsOwnHull(ship) ? session.HostEpoch : stream?.AcceptedEpoch ?? 0,
                helmPilot = engine.HasHelmPilot(ship.Hull),
                swapPhase = hullAuthority.SwapPhaseOf(ship.ShipId),
                hp = condition?.HitPoints,
                sailHp = condition?.SailHitPoints,
                fireHp = condition?.FireHitPoints,
                sinking = condition?.SinkingState,
                isSunk = condition?.SinkingState == BattleShipCondition.Sunk,
                conditionRevision = conditions.RevisionOf(ship.ShipId),
                reserve = HullReserve(ship.Hull),
            };
        }).ToArray(),
    };

    public object InspectRopes() => new
    {
        finalRopesSent,
        finalRopesAccepted,
        finalRopesRejected,
        stations = engine.InspectRopes(Registry.Ships.Select(ship => ship.Hull).ToArray(), ShipIdOf),
    };

    /// <summary>
    /// A ship not registered here yet: its waiting spawn record and the owner's latest condition, sample and final ropes,
    /// applied in that order once the copy spawns.
    /// </summary>
    private sealed class HeldShip
    {
        public BattleShipSpawnData Record;
        /// <summary>The record's place in arrival order, so copies spawn in the order their records came.</summary>
        public long RecordOrder;
        public NetworkShipCondition Condition;
        public NetworkBattleShipSample Sample;
        public NetworkBattleRopeFinal FinalRopes;
        public int FailedAttempts;
        public float RetryAt;
        public bool HeldForDeployment;

        public void SettleRecord()
        {
            Record = null;
            FailedAttempts = 0;
            HeldForDeployment = false;
        }
    }

    private sealed class ShipStream
    {
        public NetworkBattleShipSample Target;
        /// <summary>This client's mission clock when <see cref="Target"/> arrived.</summary>
        public float TargetReceivedAt;
        public MatrixFrame Start;
        public MatrixFrame Written;
        public bool HasWritten;
        public float Elapsed;
        public long Sent;
        public long Accepted;
        public int AcceptedEpoch;
        public long Applied;
        public long Rejects;
        public string LastReject;
        /// <summary>The fieldable reserve in the last accepted sample, until the hull's owner leaves or its authority moves.</summary>
        public int ReportedReserve;
        public bool HasReportedReserve;

        public void ForgetReportedReserve()
        {
            ReportedReserve = 0;
            HasReportedReserve = false;
        }

        public void Reject(string reason)
        {
            Rejects++;
            LastReject = reason;
        }
    }
}
