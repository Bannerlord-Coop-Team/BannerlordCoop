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

    /// <summary>[Game thread] The registered hull an on-foot agent stands on and its hull-local position.</summary>
    bool TryGetDeckPose(Agent agent, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal);

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
    private readonly INavalShipEngine engine;
    private readonly IBattleTeamResolver teamResolver;
    private readonly IObjectManager objectManager;
    private readonly IHostEpochPolicy hostEpochPolicy;
    private readonly IBattleResultCommitter resultCommitter;

    private readonly Dictionary<Guid, ShipStream> streams = new Dictionary<Guid, ShipStream>();
    private readonly List<BattleShipSpawnData> pendingForeignHulls = new List<BattleShipSpawnData>();
    private readonly List<HullSwap> hullSwaps = new List<HullSwap>();
    private readonly Dictionary<Guid, NetworkShipCondition> pendingConditions = new Dictionary<Guid, NetworkShipCondition>();
    private readonly Dictionary<Guid, FailedHullSpawn> failedHullSpawns = new Dictionary<Guid, FailedHullSpawn>();
    // The latest sample and final ropes of a hull whose spawn record waits, applied as soon as the copy spawns.
    private readonly Dictionary<Guid, NetworkBattleShipSample> heldSamples = new Dictionary<Guid, NetworkBattleShipSample>();
    private readonly Dictionary<Guid, NetworkBattleRopeFinal> heldFinalRopes = new Dictionary<Guid, NetworkBattleRopeFinal>();
    private readonly HashSet<Guid> heldForDeployment = new HashSet<Guid>();
    // Departures come over the campaign connection and samples over the mission mesh, so a departed owner's late sample is not
    // ordered behind its departure; its reserve stays dropped until the controller enters again.
    private readonly HashSet<string> departedControllers = new HashSet<string>();
    // The host AI hulls last migrated to, so an AI copy whose record was still waiting registers under it too.
    private string npcHullHost;
    private bool spawningForeignHull;
    private bool spawnRecordsSent;
    private float sendElapsed;
    private float elapsed;
    private readonly Dictionary<Guid, DeckSpeedSample> deckSpeeds = new Dictionary<Guid, DeckSpeedSample>();
    private long finalRopesSent, finalRopesAccepted, finalRopesRejected;

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    internal Guid ShipIdOf(MissionObject hull) => Registry.TryGetByHull(hull, out var ship) ? ship.ShipId : Guid.Empty;

    internal MissionObject HullOf(Guid shipId) => Registry.TryGet(shipId, out var ship) ? ship.Hull : null;

    public BattleShipReplicator(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IBattleSession session,
        IBattleDeploymentCoordinator deployment,
        ICoopMissionComponent missionComponent,
        INavalShipEngine engine,
        IBattleTeamResolver teamResolver,
        IObjectManager objectManager,
        IHostEpochPolicy hostEpochPolicy,
        IBattleResultCommitter resultCommitter)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.deployment = deployment;
        this.missionComponent = missionComponent;
        this.engine = engine;
        this.teamResolver = teamResolver;
        this.objectManager = objectManager;
        this.hostEpochPolicy = hostEpochPolicy;
        this.resultCommitter = resultCommitter;

        missionComponent.AgentMovementHandler.ConfigureShipDecks(CaptureDeck, ResolveDeckFrame);
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
        missionComponent.AgentMovementHandler.ConfigureShipDecks(null, null);
        messageBroker.Unsubscribe<ShipSpawnedInBattle>(Handle_ShipSpawned);
        messageBroker.Unsubscribe<NetworkSpawnBattleShips>(Handle_NetworkSpawnBattleShips);
        messageBroker.Unsubscribe<NetworkBattleShipSample>(Handle_NetworkBattleShipSample);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<NetworkBattleRopeFinal>(Handle_NetworkBattleRopeFinal);
        messageBroker.Unsubscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
        messageBroker.Unsubscribe<NetworkShipCondition>(Handle_NetworkShipCondition);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);
        hullSwaps.Clear();
        pendingConditions.Clear();
        pendingForeignHulls.Clear();
        failedHullSpawns.Clear();
        heldSamples.Clear();
        heldFinalRopes.Clear();
        heldForDeployment.Clear();
        departedControllers.Clear();
    }

    // [Game thread] Published synchronously by the spawn postfix. Hulls this client fields go on PlayerTeam; the
    // copies and replacement hulls this replicator spawns raise the same event and are registered by their spawn path instead.
    private void Handle_ShipSpawned(MessagePayload<ShipSpawnedInBattle> payload)
    {
        var hull = payload.What.Hull;
        var formation = payload.What.Formation;
        if (spawningForeignHull || hull == null || formation?.Team == null || formation.Team != Mission.Current?.PlayerTeam)
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
        if (!BattleAuthorityMigrator.AppliesHostMigration(resultCommitter, isNavalMission: true)) return;

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
        bool wasOwn = IsOwnHull(ship);
        ship.CurrentAuthority = newHost;
        bool isOwn = IsOwnHull(ship);
        if (isOwn && !wasOwn)
            ReplaceNpcHull(ship.ShipId);
        else if (wasOwn && !isOwn)
            ReleaseNpcHull(ship);

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
        // A sunk copy is disabled and simulates nothing, so it stays as it is under its new owner.
        if (engine.ReadCondition(ship.Hull)?.SinkingState == BattleShipCondition.Sunk) return "The hull has sunk.";
        if (hullSwaps.Any(swap => swap.ShipId == shipId)) return "The hull is already being replaced.";

        hullSwaps.Add(new HullSwap(shipId));
        return null;
    }

    public bool IsReplacingHull(Guid shipId) => hullSwaps.Any(swap => swap.ShipId == shipId);

    // A hull handed away mid-swap gets its parked agents back before it turns into a copy.
    private void ReleaseNpcHull(NetworkShipInfo ship)
    {
        var swap = hullSwaps.FirstOrDefault(pending => pending.ShipId == ship.ShipId);
        if (swap != null)
        {
            hullSwaps.Remove(swap);
            if (swap.Agents != null) engine.BoardHullAgents(ship.Hull, swap.Agents);
        }

        engine.ReleaseNpcHull(ship.Hull);
    }

    // [Mission tick] One step per hull per tick: vanilla removes no hull its kept agents stand on, and crews a mid-mission
    // hull the tick after spawning it (Quest5 CallReinforcement, then InitializeReinforcement).
    private void AdvanceHullSwaps()
    {
        foreach (var swap in hullSwaps.ToArray())
        {
            if (!Registry.TryGet(swap.ShipId, out var ship))
            {
                hullSwaps.Remove(swap);
                if (swap.Agents != null) engine.BoardHullAgents(null, swap.Agents);
                Logger.Error("[NavalSync] Hull swap {ShipId}: the ship went away mid-swap", swap.ShipId);
                continue;
            }

            try
            {
                AdvanceHullSwap(swap, ship);
            }
            catch (Exception exception)
            {
                hullSwaps.Remove(swap);
                Logger.Error(exception, "[NavalSync] Hull swap {ShipId} failed in phase {Phase}", swap.ShipId, swap.Phase);
                // Boarding is the step that threw in the last phase, so only an earlier failure hands the parked agents back.
                if (swap.Phase == HullSwapPhase.ReplaceHull && swap.Agents != null) ReturnParkedAgents(swap, ship);
            }
        }
    }

    private void ReturnParkedAgents(HullSwap swap, NetworkShipInfo ship)
    {
        try
        {
            engine.BoardHullAgents(ship.Hull, swap.Agents);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "[NavalSync] Hull swap {ShipId}: could not hand the parked agents back", swap.ShipId);
        }
    }

    private void AdvanceHullSwap(HullSwap swap, NetworkShipInfo ship)
    {
        switch (swap.Phase)
        {
            case HullSwapPhase.ParkAgents:
                // A hull removed during deployment is pooled and spawning its ship again returns that same kinematic copy.
                if (engine.IsDeploymentMode) return;

                swap.Agents = engine.ParkHullAgents(ship.Hull);
                swap.Phase = HullSwapPhase.ReplaceHull;
                Logger.Information("[NavalSync] Hull swap {ShipId}: parked {Count} agent(s) off the hull", ship.ShipId, swap.Agents.Count);
                return;

            case HullSwapPhase.ReplaceHull:
                MissionObject fresh;
                spawningForeignHull = true;
                try
                {
                    fresh = engine.ReplaceHull(ship.Hull);
                }
                finally
                {
                    spawningForeignHull = false;
                }

                if (fresh == null)
                {
                    hullSwaps.Remove(swap);
                    engine.BoardHullAgents(ship.Hull, swap.Agents);
                    Logger.Error("[NavalSync] Hull swap {ShipId}: the hull could not be replaced; its agents went back aboard", ship.ShipId);
                    return;
                }

                if (!Registry.TryRebindHull(ship.ShipId, fresh))
                    Logger.Error("[NavalSync] Could not rebind AI hull {ShipId} to its replacement", ship.ShipId);
                swap.Phase = HullSwapPhase.BoardAgents;
                Logger.Information("[NavalSync] Hull swap {ShipId}: replaced the hull with a simulated one", ship.ShipId);
                return;

            case HullSwapPhase.BoardAgents:
                hullSwaps.Remove(swap);
                engine.BoardHullAgents(ship.Hull, swap.Agents);
                Logger.Information("[NavalSync] Hull swap {ShipId}: boarded {Count} agent(s) onto the new hull", ship.ShipId, swap.Agents.Count);
                return;
        }
    }

    public void Tick(float dt)
    {
        elapsed += Math.Max(0f, dt);
        AdvanceHullSwaps();
        PruneRemovedHulls();
        DrainPendingForeignHulls();
        DrainPendingConditions();

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
            if (stream.Condition?.SinkingState == BattleShipCondition.Sunk) continue;

            stream.Sent++;
            network.SendAll(new NetworkBattleShipSample(ship.ShipId, session.OwnControllerId, stream.Sent,
                NetworkBattleShipSample.FromFrame(engine.GetFrame(ship.Hull)), engine.ReadInput(ship.Hull),
                engine.CaptureRopes(ship.Hull, ShipIdOf), session.HostEpoch, engine.ReadSpawnableReserve(ship.Hull)));
            SendConditionIfChanged(ship, stream);
        }
    }

    // Owners send a hull's damage state when it changes; the first check after the hull was announced sends it once.
    private void SendConditionIfChanged(NetworkShipInfo ship, ShipStream stream)
    {
        var condition = engine.ReadCondition(ship.Hull);
        if (condition == null || !condition.DiffersFrom(stream.Condition)) return;

        stream.Condition = condition;
        stream.ConditionRevision++;
        stream.ConditionEpoch = session.HostEpoch;
        network.SendAll(new NetworkShipCondition(ship.ShipId, session.OwnControllerId, stream.ConditionRevision, session.HostEpoch,
            condition));
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
            if (!pendingConditions.TryGetValue(condition.ShipId, out var pending) || IsNewerCondition(condition, pending))
                pendingConditions[condition.ShipId] = condition;
            return;
        }

        var stream = GetStream(condition.ShipId);
        var rejection = ValidateCondition(ship, session.OwnControllerId, condition, stream.ConditionRevision, stream.ConditionEpoch);
        if (rejection == null && ship.IsNpcParty && hostEpochPolicy.IsStale(condition.HostEpoch, session.HostEpoch))
            rejection = "stale_epoch";
        if (rejection != null)
        {
            stream.Reject("condition_" + rejection);
            return;
        }

        TakeCondition(ship, stream, condition);
    }

    private void TakeCondition(NetworkShipInfo ship, ShipStream stream, NetworkShipCondition condition)
    {
        engine.ApplyCondition(ship.Hull, condition.Condition);
        stream.Condition = condition.Condition;
        stream.ConditionRevision = condition.Revision;
        if (ship.IsNpcParty) stream.ConditionEpoch = condition.HostEpoch;
    }

    private static bool IsNewerCondition(NetworkShipCondition condition, NetworkShipCondition than) =>
        condition.HostEpoch > than.HostEpoch || (condition.HostEpoch == than.HostEpoch && condition.Revision > than.Revision);

    private void DrainPendingConditions()
    {
        if (pendingConditions.Count == 0) return;

        foreach (var shipId in pendingConditions.Keys.ToArray())
        {
            if (!Registry.TryGet(shipId, out _)) continue;

            var condition = pendingConditions[shipId];
            pendingConditions.Remove(shipId);
            AcceptCondition(condition);
        }
    }

    /// <summary>Why a condition must be dropped, or null to apply it.</summary>
    internal static string ValidateCondition(NetworkShipInfo ship, string ownControllerId, NetworkShipCondition condition,
        long acceptedRevision, int acceptedEpoch)
    {
        if (ship.CurrentAuthority == ownControllerId) return "own_hull";
        if (condition.OwnerControllerId != ship.CurrentAuthority) return "not_authority";

        // A new host numbers an AI hull's conditions on from its own count, so a newer epoch restarts the order.
        if (ship.IsNpcParty && condition.HostEpoch < acceptedEpoch) return "stale_epoch";
        bool newEpoch = ship.IsNpcParty && condition.HostEpoch > acceptedEpoch;
        if (!newEpoch && condition.Revision <= acceptedRevision) return "stale";
        if (condition.Condition == null || !condition.Condition.IsValid) return "invalid_condition";
        return null;
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
            if (IsAwaitingSpawn(final.ShipId)) heldFinalRopes[final.ShipId] = final;
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
        if (ship.CurrentAuthority == ownControllerId) return "own_hull";
        if (final.OwnerControllerId != ship.CurrentAuthority) return "not_authority";
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
        pendingForeignHulls.AddRange(records.Where(record => record != null));
        DrainPendingForeignHulls();
    }

    private bool IsAwaitingSpawn(Guid shipId) => pendingForeignHulls.Any(record => record.ShipId == shipId);

    private void DrainPendingForeignHulls()
    {
        if (pendingForeignHulls.Count == 0) return;
        pendingForeignHulls.RemoveAll(TrySpawnForeignHull);
    }

    // [Game thread] True when the record is settled (spawned or dropped), false to retry next tick.
    private bool TrySpawnForeignHull(BattleShipSpawnData data)
    {
        if (Mission.Current == null || session.IsOwn(data.OwnerControllerId) || Registry.TryGet(data.ShipId, out _))
            return true;
        if (!engine.AreInitialShipsDeployed) return false;

        // Vanilla's deployment finish runs over every hull and pools removed ones, so a copy spawns once it is over.
        if (engine.IsDeploymentMode)
        {
            if (heldForDeployment.Add(data.ShipId))
                Logger.Information("[NavalSync] Holding foreign hull {ShipId} ({Hull}) of {Owner} until local deployment ends",
                    data.ShipId, data.HullId, data.OwnerControllerId);
            return false;
        }

        var team = teamResolver.ResolveReplicatedTeam(data.Side, isOwn: false);
        if (team == null) return false;
        if (failedHullSpawns.TryGetValue(data.ShipId, out var failed) && elapsed < failed.RetryAt) return false;

        MissionObject hull;
        Formation formation;
        spawningForeignHull = true;
        try
        {
            hull = engine.SpawnForeignHull(data, team, out formation);
        }
        finally
        {
            spawningForeignHull = false;
        }

        if (hull == null) return GiveUpForeignHullSpawn(data, team);

        failedHullSpawns.Remove(data.ShipId);
        bool wasHeld = heldForDeployment.Remove(data.ShipId);
        var ship = new NetworkShipInfo(data.ShipId, data.OwnerControllerId, data.MapEventPartyId, data.IsNpcParty, hull, formation);
        bool registered = Registry.TryRegister(ship);
        Logger.Information("[NavalSync] Spawned foreign hull {ShipId} ({Hull}) of {Owner} on team {Team} formation {Formation}{Held}",
            data.ShipId, data.HullId, data.OwnerControllerId, team.TeamSide, formation?.FormationIndex,
            wasHeld ? " after local deployment" : "");
        // A host migration while the record waited only moved the registered AI hulls.
        if (registered && ship.IsNpcParty && npcHullHost != null && ship.CurrentAuthority != npcHullHost)
        {
            ApplyDepartedHostState(ship);
            TransferNpcHull(ship, npcHullHost);
        }

        ApplyHeldState(data.ShipId);
        return true;
    }

    // The departed host's last held state goes on before the move, so the new host takes over from its pose and damage.
    // Its epoch is behind by now, so only the authority, order and validity checks apply.
    private void ApplyDepartedHostState(NetworkShipInfo ship)
    {
        var departedHost = ship.CurrentAuthority;
        var stream = GetStream(ship.ShipId);
        if (pendingConditions.TryGetValue(ship.ShipId, out var condition) && condition.OwnerControllerId == departedHost)
        {
            pendingConditions.Remove(ship.ShipId);
            if (ValidateCondition(ship, session.OwnControllerId, condition, stream.ConditionRevision, stream.ConditionEpoch) == null)
                TakeCondition(ship, stream, condition);
        }

        if (heldSamples.TryGetValue(ship.ShipId, out var sample) && sample.OwnerControllerId == departedHost)
        {
            heldSamples.Remove(ship.ShipId);
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

        if (heldFinalRopes.TryGetValue(ship.ShipId, out var final) && final.OwnerControllerId == departedHost)
        {
            heldFinalRopes.Remove(ship.ShipId);
            AcceptFinalRopes(final);
        }
    }

    // The owner's latest state received while the copy waited, so it does not sit at the record's frame until the next sample.
    private void ApplyHeldState(Guid shipId)
    {
        if (pendingConditions.TryGetValue(shipId, out var condition))
        {
            pendingConditions.Remove(shipId);
            AcceptCondition(condition);
        }

        if (heldSamples.TryGetValue(shipId, out var sample))
        {
            heldSamples.Remove(shipId);
            AcceptSample(sample);
        }

        if (heldFinalRopes.TryGetValue(shipId, out var final))
        {
            heldFinalRopes.Remove(shipId);
            AcceptFinalRopes(final);
        }
    }

    // [Game thread] Counts a failed spawn; true once the hull is dropped, which also drops the crew buffered for it.
    private bool GiveUpForeignHullSpawn(BattleShipSpawnData data, Team team)
    {
        failedHullSpawns.TryGetValue(data.ShipId, out var failed);
        int attempts = failed.Attempts + 1;
        if (!IsForeignHullSpawnExhausted(attempts))
        {
            failedHullSpawns[data.ShipId] = new FailedHullSpawn(attempts, elapsed + ForeignHullRetrySeconds);
            return false;
        }

        failedHullSpawns.Remove(data.ShipId);
        heldForDeployment.Remove(data.ShipId);
        heldSamples.Remove(data.ShipId);
        heldFinalRopes.Remove(data.ShipId);
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
            if (IsAwaitingSpawn(sample.ShipId)) HoldSample(sample);
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
        if (!stream.HasReportedReserve || stream.Condition?.SinkingState == BattleShipCondition.Sunk) return 0;

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
        if (!heldSamples.TryGetValue(sample.ShipId, out var held) || IsNewerSample(sample, held))
            heldSamples[sample.ShipId] = sample;
    }

    internal static bool IsNewerSample(NetworkBattleShipSample sample, NetworkBattleShipSample than) =>
        sample.HostEpoch > than.HostEpoch || (sample.HostEpoch == than.HostEpoch && sample.Sequence > than.Sequence);

    /// <summary>Why a sample must be dropped, or null to accept it.</summary>
    internal static string ValidateSample(NetworkShipInfo ship, string ownControllerId, NetworkBattleShipSample sample,
        long acceptedSequence, int acceptedEpoch)
    {
        if (ship.CurrentAuthority == ownControllerId) return "own_hull";
        if (sample.OwnerControllerId != ship.CurrentAuthority) return "not_authority";

        // A new host numbers an AI hull's samples from its own stream, so a newer epoch restarts the sequence.
        if (ship.IsNpcParty && sample.HostEpoch < acceptedEpoch) return "stale_epoch";
        bool newEpoch = ship.IsNpcParty && sample.HostEpoch > acceptedEpoch;
        if (!newEpoch && sample.Sequence <= acceptedSequence) return "stale";
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

    // [Game thread] Any owned on-foot agent on a registered hull sends a hull-local pose, so it rides the deck on every peer.
    internal bool CaptureDeck(CoopAgentInfo info, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal, out float deckSpeed)
    {
        deckSpeed = 0f;
        if (!TryGetDeckPose(info?.Agent, worldPosition, out deckShip, out deckLocal)) return false;

        deckSpeed = DeckSpeed(info.AgentId, deckShip, deckLocal);
        return true;
    }

    /// <summary>[Game thread] The registered hull an on-foot agent stands on and its hull-local position.</summary>
    public bool TryGetDeckPose(Agent agent, Vec3 worldPosition, out Guid deckShip, out Vec3 deckLocal)
    {
        deckShip = Guid.Empty;
        deckLocal = Vec3.Zero;
        var hull = engine.GetSupportHull(agent);
        if (hull == null || !Registry.TryGetByHull(hull, out var ship)) return false;

        var frame = engine.GetFrame(hull);
        if (!NetworkBattleShipSample.IsValidFrame(NetworkBattleShipSample.FromFrame(frame))) return false;

        deckLocal = frame.TransformToLocalNonOrthogonal(worldPosition);
        if (!deckLocal.IsValid) return false;

        deckShip = ship.ShipId;
        return true;
    }

    // Finite difference of hull-local positions, so the puppet's walk throttle never includes hull motion.
    private float DeckSpeed(Guid agentId, Guid shipId, Vec3 local)
    {
        deckSpeeds.TryGetValue(agentId, out var previous);
        float dt = elapsed - previous.Time;
        if (previous.Ship != shipId || dt < 0f || dt > 0.5f)
        {
            deckSpeeds[agentId] = new DeckSpeedSample(shipId, local, elapsed, 0f);
            return 0f;
        }

        if (dt < 0.005f) return previous.Speed;

        float speed = (local - previous.Local).AsVec2.Length / dt;
        deckSpeeds[agentId] = new DeckSpeedSample(shipId, local, elapsed, speed);
        return speed;
    }

    // [Game thread] The local hull, owned or copied, that a received deck pose is relative to.
    internal bool ResolveDeckFrame(Agent agent, Guid deckShip, out MatrixFrame hullFrame)
    {
        hullFrame = default;
        if (!Registry.TryGet(deckShip, out var ship)) return false;

        hullFrame = engine.GetFrame(ship.Hull);
        return NetworkBattleShipSample.IsValidFrame(NetworkBattleShipSample.FromFrame(hullFrame));
    }

    private readonly struct DeckSpeedSample
    {
        public DeckSpeedSample(Guid ship, Vec3 local, float time, float speed)
        {
            Ship = ship;
            Local = local;
            Time = time;
            Speed = speed;
        }

        public Guid Ship { get; }
        public Vec3 Local { get; }
        public float Time { get; }
        public float Speed { get; }
    }

    private readonly struct FailedHullSpawn
    {
        public FailedHullSpawn(int attempts, float retryAt)
        {
            Attempts = attempts;
            RetryAt = retryAt;
        }

        public int Attempts { get; }
        public float RetryAt { get; }
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
        pendingForeignHulls = pendingForeignHulls.Count,
        heldForDeployment = heldForDeployment.Count,
        heldSamples = heldSamples.Count,
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
                swapPhase = hullSwaps.FirstOrDefault(swap => swap.ShipId == ship.ShipId)?.Phase.ToString(),
                hp = condition?.HitPoints,
                sailHp = condition?.SailHitPoints,
                fireHp = condition?.FireHitPoints,
                sinking = condition?.SinkingState,
                isSunk = condition?.SinkingState == BattleShipCondition.Sunk,
                conditionRevision = stream?.ConditionRevision ?? 0,
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

    private enum HullSwapPhase
    {
        ParkAgents,
        ReplaceHull,
        BoardAgents,
    }

    private sealed class HullSwap
    {
        public HullSwap(Guid shipId)
        {
            ShipId = shipId;
        }

        public Guid ShipId { get; }
        public HullSwapPhase Phase { get; set; }
        public IReadOnlyList<HullSwapAgent> Agents { get; set; }
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
        /// <summary>The owner's last sent, or a copy's last applied, damage state, its revision and its host epoch.</summary>
        public BattleShipCondition Condition;
        public long ConditionRevision;
        public int ConditionEpoch;
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
