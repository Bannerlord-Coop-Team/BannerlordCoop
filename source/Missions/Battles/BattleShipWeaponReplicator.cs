using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Replicates ship siege weapons by hull authority: only the hull's owner simulates its weapon and sends its state and each
/// shot, keyed by ship id and weapon path, which peers replay on their copy.
/// </summary>
public interface IBattleShipWeaponReplicator : IDisposable
{
    /// <summary>[Game thread] Hands each weapon to its hull's owner, carrying a copy's reload into one taken over in place, and
    /// sends the own weapons' state on change and again each second.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] Weapon per hull and the shot and state counters (diagnostics).</summary>
    object Inspect();
}

/// <inheritdoc cref="IBattleShipWeaponReplicator"/>
public class BattleShipWeaponReplicator : IBattleShipWeaponReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipWeaponReplicator>();

    internal const float SendIntervalSeconds = 0.1f;
    // Weapon state goes out on change; this resend lets a peer that dropped a state around a host change catch up.
    internal const float KeyframeSeconds = 1f;
    // A grapeshot volley launches several projectiles in one frame; their release swing and sound play once.
    internal const float VolleySeconds = 0.25f;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly INavalShipEngine engine;
    private readonly INavalHullAuthority hullAuthority;
    private readonly IHostEpochPolicy hostEpochPolicy;
    private readonly IMissionContext missionContext;
    private readonly IBattleShipArrivals arrivals;

    private readonly Dictionary<Guid, WeaponStream> streams = new Dictionary<Guid, WeaponStream>();
    // The latest state of a hull that is not spawned here yet, applied once it registers.
    private readonly Dictionary<Guid, NetworkShipWeaponState> pendingStates = new Dictionary<Guid, NetworkShipWeaponState>();
    private List<MissionObject> simulatedWeapons;
    private float sendElapsed;
    private float elapsed;
    private bool disposed;
    private long shotsSent, shotsReplayed, shotsDropped, statesSent, statesApplied, statesDropped;
    private string lastDrop;

    public BattleShipWeaponReplicator(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session,
        ICoopMissionComponent missionComponent, INavalShipEngine engine, INavalHullAuthority hullAuthority, IHostEpochPolicy hostEpochPolicy,
        IMissionContext missionContext, IBattleShipArrivals arrivals)
    {
        this.missionContext = missionContext;
        this.arrivals = arrivals;
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.missionComponent = missionComponent;
        this.engine = engine;
        this.hullAuthority = hullAuthority;
        this.hostEpochPolicy = hostEpochPolicy;

        messageBroker.Subscribe<SiegeWeaponFired>(Handle_SiegeWeaponFired);
        messageBroker.Subscribe<NetworkShipWeaponShot>(Handle_NetworkShipWeaponShot);
        messageBroker.Subscribe<NetworkShipWeaponState>(Handle_NetworkShipWeaponState);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
    }

    // Drops the subscriptions and the held states, and hands weapon simulation back to the defaults.
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        messageBroker.Unsubscribe<SiegeWeaponFired>(Handle_SiegeWeaponFired);
        messageBroker.Unsubscribe<NetworkShipWeaponShot>(Handle_NetworkShipWeaponShot);
        messageBroker.Unsubscribe<NetworkShipWeaponState>(Handle_NetworkShipWeaponState);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        streams.Clear();
        pendingStates.Clear();
        engine.ClearSimulatedWeapons();
    }

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    // Restarts and fills the streams before the simulation claim, so a stream's IsOwn always matches the weapons simulated here.
    public void Tick(float dt)
    {
        if (disposed) return;

        elapsed += Math.Max(0f, dt);
        RestartChangedStreams();
        FindMissingWeapons();
        UpdateSimulatedWeapons();
        DrainPendingStates();
        SendOwnStates(dt);
    }

    // A hull swap rebinds the ship to a fresh hull, and an authority move changes who simulates and numbers the states, so both
    // restart the stream; a removed hull's stream goes.
    private void RestartChangedStreams()
    {
        foreach (var shipId in streams.Keys.ToArray())
        {
            if (!Registry.TryGet(shipId, out _)) streams.Remove(shipId);
        }

        foreach (var ship in Registry.Ships)
        {
            bool isOwn = IsSimulatedHere(ship);
            if (streams.TryGetValue(ship.ShipId, out var stream) && stream.Hull == ship.Hull && stream.IsOwn == isOwn
                && stream.Authority == ship.CurrentAuthority)
                continue;

            if (stream != null && stream.Hull == ship.Hull && !stream.IsOwn && isOwn) CarryCopyState(stream);
            streams[ship.ShipId] = new WeaponStream(ship.Hull, isOwn, ship.CurrentAuthority, stream?.SentRevision ?? 0);
        }
    }

    // A copy this client takes over in place (a capture's adopt) starts simulating from its owner's last state, loaded with the
    // reload's bolt spent, as a swap carries it; applied while the weapon is still a copy, before the simulation claim.
    private void CarryCopyState(WeaponStream copy)
    {
        var carried = CarriedState(copy.Applied);
        if (copy.Weapon == null || carried == null) return;

        engine.ApplyWeaponState(copy.Weapon, carried, copy.Applied);
    }

    /// <summary>
    /// The state a weapon taken over mid-reload starts from: loaded, with one bolt fewer (never below 0), since vanilla spends
    /// the reload's bolt only once it ends. Null for a loaded weapon or none, which keeps its state.
    /// </summary>
    internal static BattleShipWeaponState CarriedState(BattleShipWeaponState applied)
    {
        const int idle = (int)RangedSiegeWeapon.WeaponState.Idle;
        if (applied == null || applied.WeaponState == idle) return null;

        return new BattleShipWeaponState(applied.Direction, applied.ReleaseAngle, idle, Math.Max(0, applied.AmmoCount - 1));
    }

    // A hull that registered before its weapon was found is looked up again.
    private void FindMissingWeapons()
    {
        foreach (var ship in Registry.Ships)
        {
            if (!streams.TryGetValue(ship.ShipId, out var stream) || stream.Weapon != null) continue;
            if (!engine.TryGetWeapon(ship.Hull, out var weapon, out var key)) continue;

            stream.Weapon = weapon;
            stream.Key = key;
        }
    }

    // Whether this client simulates the hull's weapon: it holds the hull's authority and the hull isn't being swapped.
    private bool IsSimulatedHere(NetworkShipInfo ship) =>
        ship.CurrentAuthority == session.OwnControllerId && !hullAuthority.IsReplacing(ship.ShipId);

    // Hands the engine the weapons this client simulates whenever that set changes.
    private void UpdateSimulatedWeapons()
    {
        var own = streams.Values.Where(stream => stream.IsOwn && stream.Weapon != null).Select(stream => stream.Weapon).ToList();
        if (simulatedWeapons != null && own.Count == simulatedWeapons.Count && !own.Except(simulatedWeapons).Any()) return;

        simulatedWeapons = own;
        engine.SetSimulatedWeapons(own);
        Logger.Information("[NavalSync] This client now simulates {Count} ship weapon(s)", own.Count);
    }

    // Sends each own weapon's state when it changed, or again once a keyframe interval passed, at most every send interval.
    private void SendOwnStates(float dt)
    {
        sendElapsed += Math.Max(0f, dt);
        if (sendElapsed < SendIntervalSeconds) return;
        sendElapsed = 0f;

        foreach (var entry in streams)
        {
            var stream = entry.Value;
            if (!stream.IsOwn || stream.Weapon == null) continue;

            var state = engine.ReadWeaponState(stream.Weapon);
            if (state == null || !(state.DiffersFrom(stream.Sent) || elapsed - stream.SentAt >= KeyframeSeconds)) continue;

            stream.Sent = state;
            stream.SentAt = elapsed;
            stream.SentRevision++;
            network.SendAll(new NetworkShipWeaponState(entry.Key, stream.Key, session.OwnControllerId, stream.SentRevision,
                session.HostEpoch, state));
            statesSent++;
        }
    }

    // [Game thread] Published synchronously by the projectile capture of a weapon this client simulates.
    private void Handle_SiegeWeaponFired(MessagePayload<SiegeWeaponFired> payload)
    {
        if (disposed) return;

        var fire = payload.What;
        if (fire?.Weapon == null || !TryFindStream(fire.Weapon, out var shipId, out var stream) || !stream.IsOwn) return;

        var shooterId = Guid.Empty;
        if (fire.Shooter != null && missionComponent.AgentRegistry.TryGetAgentInfo(fire.Shooter, out var info))
            shooterId = info.AgentId;

        network.SendAll(new NetworkShipWeaponShot(shipId, stream.Key, session.OwnControllerId, session.HostEpoch, shooterId,
            fire.Position, fire.Direction, fire.Orientation, fire.BaseSpeed, fire.Speed, fire.MissileItem?.StringId));
        shotsSent++;
    }

    // The stream, and its ship id, whose weapon is this one.
    private bool TryFindStream(MissionObject weapon, out Guid shipId, out WeaponStream stream)
    {
        foreach (var entry in streams)
        {
            if (!ReferenceEquals(entry.Value.Weapon, weapon)) continue;

            shipId = entry.Key;
            stream = entry.Value;
            return true;
        }

        shipId = Guid.Empty;
        stream = null;
        return false;
    }

    // Queues an owner's shot for the next naval tick, in order with its states and the hull traffic: its replay adds a missile
    // to the mission, which native code reads.
    private void Handle_NetworkShipWeaponShot(MessagePayload<NetworkShipWeaponShot> payload)
    {
        var shot = payload.What;
        GameThread.RunSafe(() =>
        {
            if (disposed || Mission.Current == null) return;
            arrivals.Enqueue(() => AcceptShot(shot), nameof(Handle_NetworkShipWeaponShot));
        }, context: nameof(Handle_NetworkShipWeaponShot));
    }

    /// <summary>
    /// [Game thread] Replays an owner's shot on this client's copy of its weapon; a shot is never held for later, so one for a
    /// hull registered since the last tick is dropped.
    /// </summary>
    internal void AcceptShot(NetworkShipWeaponShot shot)
    {
        if (shot == null) return;
        if (!Registry.TryGet(shot.ShipId, out var ship))
        {
            DropShot("unknown_ship");
            return;
        }

        var rejection = ValidateShot(ship, session.OwnControllerId, shot, missionContext.ControllersInMission);
        if (rejection == null && ship.FollowsBattleHost && hostEpochPolicy.IsStale(shot.HostEpoch, session.HostEpoch))
            rejection = "stale_epoch";
        MissionObject weapon = null;
        if (rejection == null && !TryGetWeaponOf(ship, shot.WeaponKey, out weapon)) rejection = "unknown_weapon";
        if (rejection != null)
        {
            DropShot(rejection);
            return;
        }

        var stream = streams[ship.ShipId];
        bool newVolley = !stream.HasReplayed || elapsed - stream.LastReplayedAt > VolleySeconds;
        if (!engine.ReplayWeaponShot(weapon, shot, ResolveReplayShooter(shot.ShooterAgentId), newVolley))
        {
            DropShot("not_replayed");
            return;
        }

        stream.HasReplayed = true;
        stream.LastReplayedAt = elapsed;
        shotsReplayed++;
    }

    /// <summary>Why a shot must be dropped, or null to replay it.</summary>
    internal static string ValidateShot(NetworkShipInfo ship, string ownControllerId, NetworkShipWeaponShot shot,
        IReadOnlyCollection<string> controllersInMission)
    {
        var rejection = AuthorityStamp.ValidateSender(shot.OwnerControllerId, ship.CurrentAuthority, ownControllerId,
            controllersInMission);
        if (rejection != null) return rejection;
        if (!shot.IsValid) return "invalid_shot";
        return null;
    }

    /// <summary>
    /// The shooter of a replayed projectile: the shooter's puppet here, never an agent this client drives, so the hull and
    /// agent damage gates drop every hit the replay makes. Null lets the engine pick another inert puppet.
    /// </summary>
    internal Agent ResolveReplayShooter(Guid shooterAgentId)
    {
        var agents = missionComponent.AgentRegistry;
        if (shooterAgentId == Guid.Empty || !agents.TryGetAgentInfo(shooterAgentId, out var info) || info.Agent == null) return null;

        return agents.IsLocallyControlled(shooterAgentId) ? null : info.Agent;
    }

    // Counts a dropped shot and names why.
    private void DropShot(string reason)
    {
        shotsDropped++;
        lastDrop = "shot_" + reason;
    }

    // Queues an owner's weapon state for the next naval tick, behind the shot that caused it.
    private void Handle_NetworkShipWeaponState(MessagePayload<NetworkShipWeaponState> payload)
    {
        var state = payload.What;
        GameThread.RunSafe(() =>
        {
            if (disposed || Mission.Current == null) return;
            arrivals.Enqueue(() => AcceptState(state), nameof(Handle_NetworkShipWeaponState));
        }, context: nameof(Handle_NetworkShipWeaponState));
    }

    /// <summary>[Game thread] Sets an owner's weapon state on its copy; one for a hull not spawned here yet waits for it.</summary>
    internal void AcceptState(NetworkShipWeaponState state)
    {
        if (state == null) return;
        if (!Registry.TryGet(state.ShipId, out var ship) || !streams.TryGetValue(state.ShipId, out var stream))
        {
            if (Registry.IsGone(state.ShipId))
            {
                DropState("hull_gone");
                return;
            }
            if (!pendingStates.TryGetValue(state.ShipId, out var pending) || state.Stamp.IsNewer(pending.Stamp))
                pendingStates[state.ShipId] = state;
            return;
        }

        var rejection = ValidateState(ship, session.OwnControllerId, state, stream.AppliedRevision, stream.AppliedEpoch,
            missionContext.ControllersInMission);
        if (rejection == null && ship.FollowsBattleHost && hostEpochPolicy.IsStale(state.HostEpoch, session.HostEpoch))
            rejection = "stale_epoch";
        if (rejection == null && (stream.Weapon == null || stream.Key != state.WeaponKey)) rejection = "unknown_weapon";
        if (rejection != null)
        {
            DropState(rejection);
            return;
        }

        engine.ApplyWeaponState(stream.Weapon, state.State, stream.Applied);
        stream.Applied = state.State;
        stream.AppliedRevision = state.Revision;
        if (ship.FollowsBattleHost) stream.AppliedEpoch = state.HostEpoch;
        statesApplied++;
    }

    // Counts a dropped state and names why.
    private void DropState(string reason)
    {
        statesDropped++;
        lastDrop = "state_" + reason;
    }

    /// <summary>Why a weapon state must be dropped, or null to apply it.</summary>
    internal static string ValidateState(NetworkShipInfo ship, string ownControllerId, NetworkShipWeaponState state,
        long appliedRevision, int appliedEpoch, IReadOnlyCollection<string> controllersInMission)
    {
        // A new host numbers an AI hull's weapon states on from its own count, so a newer epoch restarts the order.
        var rejection = state.Stamp.Validate(ship.CurrentAuthority, ownControllerId,
            new AuthorityStamp(ship.CurrentAuthority, appliedEpoch, appliedRevision), followsHostEpoch: ship.FollowsBattleHost,
            controllersInMission);
        if (rejection != null) return rejection;
        if (state.State == null || !state.State.IsValid) return "invalid_state";
        return null;
    }

    // Applies the held states of hulls that spawned since, and forgets those of hulls that never will.
    private void DrainPendingStates()
    {
        if (pendingStates.Count == 0) return;

        foreach (var shipId in pendingStates.Keys.ToArray())
        {
            // A hull that could not spawn or left the mission never takes its state.
            if (Registry.IsGone(shipId))
            {
                pendingStates.Remove(shipId);
                continue;
            }
            if (!streams.ContainsKey(shipId)) continue;

            var state = pendingStates[shipId];
            pendingStates.Remove(shipId);
            AcceptState(state);
        }
    }

    // The hull's weapon, when its key is the one the message names.
    private bool TryGetWeaponOf(NetworkShipInfo ship, string weaponKey, out MissionObject weapon)
    {
        weapon = streams.TryGetValue(ship.ShipId, out var stream) && stream.Key == weaponKey ? stream.Weapon : null;
        return weapon != null;
    }

    // A joiner gets each own weapon's last state, which it holds until it spawns the hull's copy.
    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var entered = payload.What;
        if (entered.InstanceId != null && entered.InstanceId != session.InstanceId) return;

        GameThread.RunSafe(() =>
        {
            if (disposed || session.IsOwn(entered.ControllerId)) return;

            foreach (var entry in streams)
            {
                var stream = entry.Value;
                if (!stream.IsOwn || stream.Sent == null) continue;

                network.Send(entered.ControllerId, new NetworkShipWeaponState(entry.Key, stream.Key, session.OwnControllerId,
                    stream.SentRevision, session.HostEpoch, stream.Sent));
            }
        }, context: nameof(Handle_PeerEntered));
    }

    public object Inspect() => new
    {
        shotsSent,
        shotsReplayed,
        shotsDropped,
        statesSent,
        statesApplied,
        statesDropped,
        lastDrop,
        pendingStates = pendingStates.Count,
        simulated = simulatedWeapons?.Count ?? 0,
        weapons = streams.Select(entry => new
        {
            shipId = entry.Key,
            key = entry.Value.Key,
            hasWeapon = entry.Value.Weapon != null,
            simulatedHere = entry.Value.IsOwn,
            sentRevision = entry.Value.SentRevision,
            appliedRevision = entry.Value.AppliedRevision,
            state = entry.Value.IsOwn ? entry.Value.Sent?.WeaponState : entry.Value.Applied?.WeaponState,
            ammo = entry.Value.IsOwn ? entry.Value.Sent?.AmmoCount : entry.Value.Applied?.AmmoCount,
        }).ToArray(),
    };

    // One hull's weapon as this client sees it: who simulates it, and what was sent or applied for it.
    private sealed class WeaponStream
    {
        public WeaponStream(MissionObject hull, bool isOwn, string authority, long sentRevision)
        {
            Hull = hull;
            IsOwn = isOwn;
            Authority = authority;
            SentRevision = sentRevision;
        }

        public MissionObject Hull { get; }
        /// <summary>Whether this client simulates the weapon: the hull is its own and not mid-swap.</summary>
        public bool IsOwn { get; }
        /// <summary>The hull's authority, whose states this stream orders.</summary>
        public string Authority { get; }
        public MissionObject Weapon { get; set; }
        public string Key { get; set; }
        /// <summary>The owner's last sent state and its revision, which carries over a restart so peers keep accepting it.</summary>
        public BattleShipWeaponState Sent { get; set; }
        public long SentRevision { get; set; }
        /// <summary>This client's replicator clock when the last state went out.</summary>
        public float SentAt { get; set; }
        /// <summary>A copy's last applied state, its revision and its host epoch.</summary>
        public BattleShipWeaponState Applied { get; set; }
        public long AppliedRevision { get; set; }
        public int AppliedEpoch { get; set; }
        /// <summary>This client's replicator clock at the last replayed shot.</summary>
        public float LastReplayedAt { get; set; }
        public bool HasReplayed { get; set; }
    }
}
