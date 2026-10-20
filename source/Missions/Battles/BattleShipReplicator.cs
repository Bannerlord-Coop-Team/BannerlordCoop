using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Replicates naval hulls over the mission mesh. Own hulls register when they spawn, are announced to peers at
/// the first deployment commit and stream their frame, body motion, helm input and ropes at 20 Hz; other owners'
/// hulls are spawned as copies once this client's deployment is over and the engine steers them toward those samples.
/// Ropes ride the samples because each one carries every station's generation-ordered state, so a later sample repairs a
/// rejected one; leaving sends them once more. Owners also send a hull's damage state and fieldable reserve when either
/// changes, and copies take them on. Every stream drops traffic from a controller not in the mission. A hull that leaves
/// with its owner (retreat, leave or disconnect) is removed on every client, after its seats and deck are cleared; only the
/// AI hulls move to the next battle host.
/// </summary>
public interface IBattleShipReplicator : IDisposable
{
    /// <summary>[Game thread] Announce hulls, stream own samples and expire stale ones for this frame.</summary>
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

    /// <summary>
    /// [Game thread] Vanilla is about to remove <paramref name="hull"/> (BeforeShipRemovedEvent). For an own hull peers know,
    /// tells them it left so they remove their copy; mid-mission it also clears the deck here and sends the hull's last ropes.
    /// A hull swap, a hull not announced yet and, as the mission ends, an AI hull (it moves to the next host) send nothing.
    /// </summary>
    void AnnounceRemoval(MissionObject hull, ShipRemovalReason reason, bool missionEnding);

    /// <summary>[Game thread] How many hulls of a side left the mission with their owner, for the end check's retreat rule.</summary>
    int DepartedHulls(BattleSideEnum side);
}

/// <inheritdoc cref="IBattleShipReplicator"/>
public class BattleShipReplicator : IBattleShipReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipReplicator>();

    internal const float SendIntervalSeconds = 0.05f;
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
    private readonly IMissionContext missionContext;
    private readonly IHostEpochPolicy hostEpochPolicy;
    private readonly IBattleResultCommitter resultCommitter;
    private readonly INavalShipEngine engine;
    private readonly IBattleTeamResolver teamResolver;
    private readonly IObjectManager objectManager;
    private readonly INavalHullAuthority hullAuthority;
    private readonly IBattleShipConditionStream conditions;
    private readonly IAgentStationUseReplicator stations;
    private readonly IBattlePartyWithdrawal partyWithdrawal;

    private readonly Dictionary<Guid, ShipStream> streams = new Dictionary<Guid, ShipStream>();
    private readonly Dictionary<BattleSideEnum, int> departedHulls = new Dictionary<BattleSideEnum, int>();
    // Everything received for a ship that is not registered here yet, applied in one ordered drain once it is.
    private readonly Dictionary<Guid, HeldShip> heldShips = new Dictionary<Guid, HeldShip>();
    // [Game thread] Players that left and have not entered again; a late record of theirs never spawns a hull here.
    private readonly HashSet<string> departedOwners = new HashSet<string>();
    // [Game thread] Departed players whose party withdrawal waits for their hulls to settle, with whether each was the host.
    private readonly Dictionary<string, bool> pendingWithdrawals = new Dictionary<string, bool>();
    private long recordsHeld;
    // The host AI hulls last migrated to, so an AI copy whose record was still waiting registers under it too.
    private string npcHullHost;
    private bool spawnRecordsSent;
    private float sendElapsed;
    private float elapsed;
    private long finalRopesSent, finalRopesAccepted, finalRopesRejected;
    private long removalsSent, removalsAccepted, removalsRejected, agentsMovedOffDepartedHulls;
    private bool missionEndRopesSent;

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
        IBattleShipConditionStream conditions,
        IAgentStationUseReplicator stations)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        session = context.Session;
        deployment = context.Deployment;
        missionComponent = context.MissionComponent;
        missionContext = context.MissionContext;
        hostEpochPolicy = context.HostEpochPolicy;
        resultCommitter = context.ResultCommitter;
        partyWithdrawal = context.PartyWithdrawal;
        this.engine = engine;
        this.teamResolver = teamResolver;
        this.objectManager = objectManager;
        this.hullAuthority = hullAuthority;
        this.conditions = conditions;
        this.stations = stations;

        messageBroker.Subscribe<ShipSpawnedInBattle>(Handle_ShipSpawned);
        messageBroker.Subscribe<NetworkSpawnBattleShips>(Handle_NetworkSpawnBattleShips);
        messageBroker.Subscribe<NetworkBattleShipSample>(Handle_NetworkBattleShipSample);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Subscribe<NetworkBattleRopeFinal>(Handle_NetworkBattleRopeFinal);
        messageBroker.Subscribe<BattleHostMigrated>(Handle_BattleHostMigrated);
        messageBroker.Subscribe<NetworkShipCondition>(Handle_NetworkShipCondition);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Subscribe<NetworkShipRemoved>(Handle_NetworkShipRemoved);
    }

    // Drops every subscription and the records still waiting for a hull.
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
        messageBroker.Unsubscribe<NetworkShipRemoved>(Handle_NetworkShipRemoved);
        heldShips.Clear();
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

    // AI hulls follow the battle host. Every client moves their authority; the new host takes over simulating them and the
    // old host, if it is still here, hands them back to the samples.
    private void Handle_BattleHostMigrated(MessagePayload<BattleHostMigrated> payload)
    {
        var migrated = payload.What;
        if (migrated.MapEventId != session.InstanceId) return;
        if (!BattleAuthorityMigrator.AppliesHostMigration(resultCommitter, session.IsNaval)) return;

        GameThread.RunSafe(() => TransferHostHulls(migrated.NewHostControllerId ?? session.OwnControllerId),
            context: nameof(Handle_BattleHostMigrated));
    }

    /// <summary>[Game thread] Moves every AI hull to <paramref name="newHost"/>, and flips local simulation where ours changed.</summary>
    internal int TransferHostHulls(string newHost)
    {
        npcHullHost = newHost;
        int transferred = 0;
        foreach (var ship in Registry.Ships)
        {
            if (ship.CurrentAuthority == newHost || !ship.FollowsBattleHost) continue;

            TransferHull(ship, newHost);
            transferred++;
        }

        return transferred;
    }

    // Every client moves the hull at the same ordered event, so the new authority's stream starts from its own count.
    private void TransferHull(NetworkShipInfo ship, string newAuthority)
    {
        bool isOwn = hullAuthority.Transfer(ship, newAuthority);

        // The old owner's samples no longer describe the hull, whoever simulates it now.
        var stream = GetStream(ship.ShipId);
        stream.Target = null;
        stream.Accepted = 0;
        stream.AcceptedEpoch = 0;
        conditions.RestartAfterAuthorityMove(ship.ShipId);
        Logger.Information("[NavalSync] {Kind} hull {ShipId} of {Owner} moved to {Authority}{Local}",
            ship.IsNpcParty ? "AI" : "Player", ship.ShipId, ship.OriginalOwner, newAuthority,
            isOwn ? " (this client)" : "");
    }

    public string ReplaceNpcHull(Guid shipId)
    {
        if (!Registry.TryGet(shipId, out var ship)) return "No registered hull has that ship id.";
        if (!ship.FollowsBattleHost || !IsOwnHull(ship)) return "The hull is not an AI hull this client simulates.";
        return hullAuthority.Replace(shipId);
    }

    public bool IsReplacingHull(Guid shipId) => hullAuthority.IsReplacing(shipId);

    public void Tick(float dt)
    {
        elapsed += Math.Max(0f, dt);
        WithdrawSettledParties();
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

        ExpireStaleSamples();
    }

    // Withdraws a departed party once its hulls settled; run before the hull step, so a removal that finished last tick has its
    // unparked agents back on physics first. A returner's re-entry dropped its entry, so its fresh troops never fade.
    private void WithdrawSettledParties()
    {
        if (pendingWithdrawals.Count == 0) return;

        foreach (var entry in pendingWithdrawals.Where(entry => hullAuthority.IsSettledFor(entry.Key)).ToList())
        {
            pendingWithdrawals.Remove(entry.Key);
            partyWithdrawal.WithdrawParty(entry.Key, entry.Value);
        }
    }

    private void PruneRemovedHulls()
    {
        var live = engine.Hulls;
        foreach (var ship in Registry.Ships)
        {
            if (live.Contains(ship.Hull)) continue;

            Prune(ship.ShipId);
            Logger.Information("[NavalSync] Hull {ShipId} of {Owner} left the mission (deployment mode {DeploymentMode}); dropped it",
                ship.ShipId, ship.CurrentAuthority, engine.IsDeploymentMode);
        }
    }

    // Removed is the one signal every buffer keyed by the ship id drops on.
    private void Prune(Guid shipId)
    {
        if (!Registry.Remove(shipId)) Registry.MarkRemoved(shipId);
        streams.Remove(shipId);
        conditions.Forget(shipId);
        heldShips.Remove(shipId);
    }

    public void AnnounceRemoval(MissionObject hull, ShipRemovalReason reason, bool missionEnding)
    {
        if (!spawnRecordsSent || hullAuthority.IsSpawnRegistrationSuppressed || !Registry.TryGetByHull(hull, out var ship)
            || !IsOwnHull(ship))
            return;
        if (missionEnding && ship.FollowsBattleHost) return;

        if (!missionEnding)
        {
            // Vanilla fades the hull's own crew; puppets seated on it and other owners' agents on its deck are this client's.
            stations.ReleaseSeatsOn(hull);
            MoveDeckAgents(ship, new[] { hull });
            CountDeparted(ship);
            if (!missionEndRopesSent) SendFinalRopes(ship);
        }

        network.SendAll(new NetworkShipRemoved(ship.ShipId, session.OwnControllerId, reason, missionEnding));
        removalsSent++;
        Logger.Information("[NavalSync] Own hull {ShipId} leaves the mission ({Reason}{Ending}); told peers to remove it",
            ship.ShipId, reason, missionEnding ? ", mission ending" : "");
    }

    public int DepartedHulls(BattleSideEnum side) => departedHulls.TryGetValue(side, out int count) ? count : 0;

    private void CountDeparted(NetworkShipInfo ship)
    {
        var side = engine.SideOf(ship.Hull);
        departedHulls[side] = DepartedHulls(side) + 1;
    }

    private void Handle_NetworkShipRemoved(MessagePayload<NetworkShipRemoved> payload)
    {
        var removal = payload.What;
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            AcceptRemoval(removal);
        }, context: nameof(Handle_NetworkShipRemoved));
    }

    /// <summary>[Game thread] Removes the copy of a hull its owner took out of the mission; one not spawned here never will be.</summary>
    internal void AcceptRemoval(NetworkShipRemoved removal)
    {
        if (removal == null) return;

        if (!Registry.TryGet(removal.ShipId, out var ship))
        {
            if (heldShips.TryGetValue(removal.ShipId, out var held) && held.Record != null
                && held.Record.OwnerControllerId != removal.OwnerControllerId)
            {
                removalsRejected++;
                return;
            }

            Prune(removal.ShipId);
            removalsAccepted++;
            return;
        }

        if (AuthorityStamp.ValidateSender(removal.OwnerControllerId, ship.CurrentAuthority, session.OwnControllerId,
            missionContext.ControllersInMission) != null)
        {
            removalsRejected++;
            return;
        }

        // An owner still in the battle keeps the crew its removal left alive, and the crew it fades reaches peers as routs.
        RemoveHulls(new[] { ship }, removal.Reason, removal.MissionEnding ? DepartingCrew.All : DepartingCrew.None);
        removalsAccepted++;
    }

    // [Game thread] Every client's half of hulls leaving with their owner. Seats go first, since no machine of a removed hull
    // may keep a user (R33); then other owners' agents move to their own hull; the rest are parked a tick before the
    // departing crew withdraws and the hulls go.
    private void RemoveHulls(IReadOnlyList<NetworkShipInfo> departing, ShipRemovalReason reason, DepartingCrew crew)
    {
        if (departing.Count == 0) return;

        var departingHulls = departing.Select(ship => ship.Hull).ToList();
        foreach (var ship in departing)
        {
            stations.ReleaseSeatsOn(ship.Hull);
            engine.ReleaseHullStations(ship.Hull);
        }

        foreach (var ship in departing)
            MoveDeckAgents(ship, departingHulls);

        hullAuthority.Remove(departing, crew);
        foreach (var ship in departing)
        {
            CountDeparted(ship);
            Prune(ship.ShipId);
            Logger.Information("[NavalSync] Hull {ShipId} of {Owner} left with its owner ({Reason}); removed it",
                ship.ShipId, ship.CurrentAuthority, reason);
        }
    }

    // A departing hull's crew leaves with it; anyone else on its deck goes to the hull they crew, or the nearest friendly one.
    private void MoveDeckAgents(NetworkShipInfo ship, IReadOnlyCollection<MissionObject> departingHulls)
    {
        // A hull queued in an earlier frame is removed with whoever stands on it then, and a swap's parked hull is replaced
        // with whoever stands on it next tick (R33, R40), so neither takes anybody in.
        var excluded = departingHulls.Concat(hullAuthority.HullsBeingRemoved).Concat(hullAuthority.HullsBeingReplaced).ToList();
        foreach (var agent in engine.AgentsOnDeck(ship.Hull))
        {
            var crewHull = engine.CrewHullOf(agent);
            if (crewHull != null && departingHulls.Contains(crewHull)) continue;

            var target = crewHull != null && !excluded.Contains(crewHull) && !engine.IsHullRemoved(crewHull)
                && !engine.IsSinking(crewHull)
                ? crewHull
                : engine.NearestFriendlyHull(agent, excluded);
            if (target == null)
            {
                Logger.Warning("[NavalSync] No friendly hull to move agent {Agent} to off departing hull {ShipId}",
                    agent.Index, ship.ShipId);
                continue;
            }

            engine.MoveAgentToHull(agent, target);
            agentsMovedOffDepartedHulls++;
        }
    }

    /// <summary>[Game thread] Removes every hull a player who left the battle owned, and drops its records still waiting here.</summary>
    internal void RemoveHullsOf(string controllerId)
    {
        // The hulls a departing host simulates for others stay and move to the next host.
        var departing = Registry.Ships.Where(ship => !ship.FollowsBattleHost && ship.CurrentAuthority == controllerId).ToList();
        RemoveHulls(departing, ShipRemovalReason.Leave, DepartingCrew.All);

        foreach (var entry in heldShips.Where(entry => entry.Value.Record != null && !entry.Value.Record.FollowsBattleHost
            && entry.Value.Record.OwnerControllerId == controllerId).ToArray())
            Prune(entry.Key);
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
                engine.CaptureRopes(ship.Hull, ShipIdOf), session.HostEpoch, engine.ReadMotion(ship.Hull)));
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
            if (Registry.StateOf(condition.ShipId) == ShipLifecycle.Removed) return;

            var held = Held(condition.ShipId);
            if (held.Condition == null || condition.Stamp.IsNewer(held.Condition.Stamp)) held.Condition = condition;
            return;
        }

        var stream = GetStream(condition.ShipId);
        var rejection = conditions.Accept(ship, condition, fromDepartedHost: false);
        if (rejection != null) stream.Reject("condition_" + rejection);
    }

    public void SendFinalRopes()
    {
        if (!spawnRecordsSent) return;

        missionEndRopesSent = true;
        foreach (var ship in Registry.Ships.Where(IsOwnHull))
            SendFinalRopes(ship);
    }

    private void SendFinalRopes(NetworkShipInfo ship)
    {
        var ropes = engine.CaptureRopes(ship.Hull, ShipIdOf);
        if (ropes == null || ropes.Length == 0) return;

        network.SendAll(new NetworkBattleRopeFinal(ship.ShipId, session.OwnControllerId, ropes));
        finalRopesSent++;
    }

    private void Handle_NetworkBattleRopeFinal(MessagePayload<NetworkBattleRopeFinal> payload)
    {
        var final = payload.What;
        GameThread.RunSafe(() => AcceptFinalRopes(final), context: nameof(Handle_NetworkBattleRopeFinal));
    }

    // A departed host's held final ropes are replayed on purpose before its AI hulls move, so they skip the membership gate.
    private void AcceptFinalRopes(NetworkBattleRopeFinal final, bool fromDepartedHost = false)
    {
        if (Mission.Current == null || final == null) return;

        if (!Registry.TryGet(final.ShipId, out var ship))
        {
            if (Registry.StateOf(final.ShipId) == ShipLifecycle.Awaiting) Held(final.ShipId).FinalRopes = final;
            else finalRopesRejected++;
            return;
        }

        if (ValidateFinalRopes(ship, session.OwnControllerId, final, fromDepartedHost ? null : missionContext.ControllersInMission) != null)
        {
            finalRopesRejected++;
            return;
        }

        engine.ApplyRopes(ship.Hull, final.Ropes, HullOf, final: true);
        finalRopesAccepted++;
    }

    /// <summary>Why a final rope state must be dropped, or null to apply it.</summary>
    internal static string ValidateFinalRopes(NetworkShipInfo ship, string ownControllerId, NetworkBattleRopeFinal final,
        IReadOnlyCollection<string> controllersInMission)
    {
        var rejection = AuthorityStamp.ValidateSender(final.OwnerControllerId, ship.CurrentAuthority, ownControllerId,
            controllersInMission);
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
            // A player who left or disconnected takes its hulls; an AI hull still spawns, then moves to the host. A returner's
            // fresh records follow its deployment commit, long after the server introduced it to every member (PeerEntered).
            if (!record.FollowsBattleHost && departedOwners.Contains(record.OwnerControllerId))
            {
                Logger.Information("[NavalSync] Dropped hull record {ShipId} of departed {Owner}", record.ShipId, record.OwnerControllerId);
                continue;
            }

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
        // A hull that left with its owner never spawns here, whatever arrives for it late.
        if (Registry.StateOf(data.ShipId) == ShipLifecycle.Removed)
        {
            heldShips.Remove(data.ShipId);
            return true;
        }

        if (Mission.Current == null || session.IsOwn(data.OwnerControllerId) || Registry.TryGet(data.ShipId, out _))
        {
            held.SettleRecord();
            return true;
        }

        // A peer's catch-up is relayed past the server's join hold, so it can beat this client learning the owner is a member.
        if (!data.FollowsBattleHost && AuthorityStamp.ValidateMember(data.OwnerControllerId, missionContext.ControllersInMission) != null)
        {
            if (!held.HeldForMembership)
            {
                held.HeldForMembership = true;
                Logger.Information("[NavalSync] Holding hull {ShipId} of {Owner} until it is a mission member", data.ShipId, data.OwnerControllerId);
            }
            return false;
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
        var ship = new NetworkShipInfo(data.ShipId, data.OwnerControllerId, data.MapEventPartyId, data.IsNpcParty, hull, formation,
            data.OriginalOwner);
        bool registered = Registry.TryRegister(ship);
        Logger.Information("[NavalSync] Spawned foreign hull {ShipId} ({Hull}) of {Owner} on team {Team} formation {Formation}{Held}",
            data.ShipId, data.HullId, data.OwnerControllerId, team.TeamSide, formation?.FormationIndex,
            held.HeldForDeployment ? " after local deployment" : "");
        // A host change while the record waited only moved the registered hulls.
        var currentAuthority = registered ? CurrentAuthorityOf(ship) : null;
        if (currentAuthority != null && currentAuthority != ship.CurrentAuthority)
        {
            ApplyDepartedOwnerState(ship, held);
            TransferHull(ship, currentAuthority);
        }

        ApplyHeldState(held);
        return true;
    }

    // Who simulates a hull whose record waited: an AI hull moves with the host, a player's stays with its owner.
    private string CurrentAuthorityOf(NetworkShipInfo ship) =>
        ship.FollowsBattleHost ? npcHullHost ?? ship.CurrentAuthority : ship.CurrentAuthority;

    // The departed owner's last held state goes on before the move, so the new authority takes over from its pose and damage.
    // Its epoch may be behind and its sender is out of the mission by now, so only the authority, order and validity checks apply.
    private void ApplyDepartedOwnerState(NetworkShipInfo ship, HeldShip held)
    {
        var departedOwner = ship.CurrentAuthority;
        var stream = GetStream(ship.ShipId);
        var condition = held.Condition;
        if (condition?.OwnerControllerId == departedOwner)
        {
            held.Condition = null;
            conditions.Accept(ship, condition, fromDepartedHost: true);
        }

        var sample = held.Sample;
        if (sample?.OwnerControllerId == departedOwner)
        {
            held.Sample = null;
            if (ValidateSample(ship, session.OwnControllerId, sample, stream.Accepted, stream.AcceptedEpoch, null) == null)
                TakeSample(ship, stream, sample);
        }

        var final = held.FinalRopes;
        if (final?.OwnerControllerId == departedOwner)
        {
            held.FinalRopes = null;
            AcceptFinalRopes(final, fromDepartedHost: true);
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

    /// <summary>[Game thread] Takes an owner's sample as the copied hull's steering target.</summary>
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

        var rejection = ValidateSample(ship, session.OwnControllerId, sample, stream.Accepted, stream.AcceptedEpoch,
            missionContext.ControllersInMission);
        // BR-102: an AI hull follows the host, so a superseded hosting generation is dropped like siege state.
        if (rejection == null && ship.FollowsBattleHost && hostEpochPolicy.IsStale(sample.HostEpoch, session.HostEpoch))
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
        if (ship.FollowsBattleHost) stream.AcceptedEpoch = sample.HostEpoch;

        engine.ApplyInput(ship.Hull, sample.Input);
        engine.ApplyRopes(ship.Hull, sample.Ropes, HullOf, final: false);
        engine.ApplyForeignSample(ship.Hull, NetworkBattleShipSample.ToFrame(sample.Frame), sample.Motion);
        stream.Target = sample;
        stream.TargetReceivedAt = elapsed;
        stream.Accepted = sample.Sequence;
    }

    // The owner reports it with the hull's condition, on change, so no frame length expires it: vanilla latches a depleted
    // side the first frame it reads one. Sinking, the owner leaving or an authority move drop it instead.
    public int ReportedReserveOf(MissionObject hull)
    {
        if (!Registry.TryGetByHull(hull, out var ship) || IsOwnHull(ship)) return 0;
        if (conditions.ConditionOf(ship.ShipId)?.SinkingState == BattleShipCondition.Sunk) return 0;

        return conditions.ReportedReserveOf(ship.ShipId) ?? 0;
    }

    // A copy reads 0 from the engine and an own hull 0 from the reports, so a hull mid-swap counts 0 either way.
    public int HullReserve(MissionObject hull) =>
        Registry.TryGetByHull(hull, out var ship) && !IsOwnHull(ship) ? ReportedReserveOf(hull) : engine.ReadSpawnableReserve(hull);

    // A player who left takes its hulls with it, whether or not its own removal reached this client first.
    // A player who left or disconnected (the server sends a naval disconnect as a leave, D4) takes its hulls; a departed
    // host's AI hulls stay and move to its successor with the host migration.
    private void Handle_MissionPeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        var left = payload.What;
        if (!IsDepartureOfThisBattle(left.ControllerId, left.InstanceId)) return;

        GameThread.RunSafe(() =>
        {
            // Read here, as the migrator does, so it comes before the successor's host assignment applied on this thread.
            bool wasHost = session.IsHostController(left.ControllerId);
            departedOwners.Add(left.ControllerId);
            ForgetReportedReserveOf(left.ControllerId);
            if (Mission.Current == null) return;

            // At sea the hull removal takes the leaver's crew, so its party is withdrawn only once the hulls are gone.
            RemoveHullsOf(left.ControllerId);
            pendingWithdrawals[left.ControllerId] = wasHost;
        }, context: nameof(Handle_MissionPeerLeft));
    }

    private bool IsDepartureOfThisBattle(string controllerId, string instanceId) =>
        !string.IsNullOrEmpty(controllerId) && (instanceId == null || instanceId == session.InstanceId) && !session.IsOwn(controllerId);

    /// <summary>
    /// [Game thread] Drops the reserve a departed controller reported for the hulls it held; the membership gate drops its late
    /// reports until it enters again.
    /// </summary>
    internal void ForgetReportedReserveOf(string controllerId)
    {
        foreach (var ship in Registry.Ships.Where(ship => ship.CurrentAuthority == controllerId))
            conditions.ForgetReserve(ship.ShipId);
    }

    private void HoldSample(NetworkBattleShipSample sample)
    {
        var held = Held(sample.ShipId);
        if (held.Sample == null || sample.Stamp.IsNewer(held.Sample.Stamp)) held.Sample = sample;
    }

    /// <summary>Why a sample must be dropped, or null to accept it.</summary>
    internal static string ValidateSample(NetworkShipInfo ship, string ownControllerId, NetworkBattleShipSample sample,
        long acceptedSequence, int acceptedEpoch, IReadOnlyCollection<string> controllersInMission)
    {
        // A new host numbers an AI hull's samples from its own stream, so a newer epoch restarts the sequence.
        var rejection = sample.Stamp.Validate(ship.CurrentAuthority, ownControllerId,
            new AuthorityStamp(ship.CurrentAuthority, acceptedEpoch, acceptedSequence), followsHostEpoch: ship.FollowsBattleHost,
            controllersInMission);
        if (rejection != null) return rejection;
        if (!sample.HasValidFrame) return "invalid_frame";
        if (!sample.Motion.IsValid) return "invalid_motion";
        if (!sample.Input.IsValid) return "invalid_input";
        if (!BattleRopeState.AreValid(sample.Ropes)) return "invalid_ropes";
        return null;
    }

    // A copy's stream stops counting as live once no sample came for the lifetime; the engine's steering ages it on its own.
    private void ExpireStaleSamples()
    {
        foreach (var stream in streams.Values)
        {
            if (stream.Target == null || elapsed - stream.TargetReceivedAt <= SampleLifetimeSeconds) continue;

            stream.Target = null;
            stream.Reject("expired");
        }
    }

    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var entered = payload.What;
        if (entered.InstanceId != null && entered.InstanceId != session.InstanceId) return;

        GameThread.RunSafe(() =>
        {
            departedOwners.Remove(entered.ControllerId);
            pendingWithdrawals.Remove(entered.ControllerId);
            if (!spawnRecordsSent || session.IsOwn(entered.ControllerId)) return;

            SendSpawnRecords(entered.ControllerId);
            // A hull's condition and reserve only go out on change, so the entrant gets the last ones too.
            foreach (var ship in Registry.Ships.Where(IsOwnHull))
                conditions.SendLastTo(entered.ControllerId, ship);
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
        removalsSent,
        removalsAccepted,
        removalsRejected,
        agentsMovedOffDepartedHulls,
        departedAttackerHulls = DepartedHulls(BattleSideEnum.Attacker),
        departedDefenderHulls = DepartedHulls(BattleSideEnum.Defender),
        pendingForeignHulls = heldShips.Values.Count(held => held.Record != null),
        heldForDeployment = heldShips.Values.Count(held => held.HeldForDeployment),
        heldSamples = heldShips.Values.Count(held => held.Sample != null),
        pendingWithdrawals = pendingWithdrawals.Keys.ToArray(),
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
                bodyMode = engine.BodyMode(ship.Hull),
                controller = engine.ControllerName(ship.Hull),
                input = engine.ReadInput(ship.Hull),
                sent = stream?.Sent ?? 0,
                accepted = stream?.Accepted ?? 0,
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
        public bool HeldForMembership;

        public void SettleRecord()
        {
            Record = null;
            FailedAttempts = 0;
            HeldForDeployment = false;
            HeldForMembership = false;
        }
    }

    private sealed class ShipStream
    {
        public NetworkBattleShipSample Target;
        /// <summary>This client's mission clock when <see cref="Target"/> arrived.</summary>
        public float TargetReceivedAt;
        public long Sent;
        public long Accepted;
        public int AcceptedEpoch;
        public long Rejects;
        public string LastReject;

        public void Reject(string reason)
        {
            Rejects++;
            LastReject = reason;
        }
    }
}
