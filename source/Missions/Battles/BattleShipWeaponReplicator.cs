using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Replicates ship siege weapons (ballista, fire ballista, mangonel) by hull authority. Only the hull's owner simulates its
/// weapon, so a copy's local AI never aims, loads or fires it. The owner sends the weapon's aim, state and ammo when they
/// change and every projectile it launches, keyed by ship id and weapon path; peers replay them on their copy. A replayed
/// projectile is presentation only: the owner's own projectile decides the hit, which reaches the struck hull's owner
/// through <see cref="BattleShipDamageRouter"/>.
/// </summary>
public interface IBattleShipWeaponReplicator : IDisposable
{
    /// <summary>[Game thread] Hands each weapon to its hull's owner and sends the own weapons' changed state.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] Weapon per hull and the shot and state counters (diagnostics).</summary>
    object Inspect();
}

/// <inheritdoc cref="IBattleShipWeaponReplicator"/>
public class BattleShipWeaponReplicator : IBattleShipWeaponReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipWeaponReplicator>();

    internal const float SendIntervalSeconds = 0.1f;
    // A grapeshot volley launches several projectiles in one frame; their release swing and sound play once.
    internal const float VolleySeconds = 0.25f;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly ICoopMissionComponent missionComponent;
    private readonly INavalShipEngine engine;
    private readonly IBattleShipReplicator ships;
    private readonly IHostEpochPolicy hostEpochPolicy;

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
        ICoopMissionComponent missionComponent, INavalShipEngine engine, IBattleShipReplicator ships, IHostEpochPolicy hostEpochPolicy)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.missionComponent = missionComponent;
        this.engine = engine;
        this.ships = ships;
        this.hostEpochPolicy = hostEpochPolicy;

        messageBroker.Subscribe<SiegeWeaponFired>(Handle_SiegeWeaponFired);
        messageBroker.Subscribe<NetworkShipWeaponShot>(Handle_NetworkShipWeaponShot);
        messageBroker.Subscribe<NetworkShipWeaponState>(Handle_NetworkShipWeaponState);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
    }

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

    public void Tick(float dt)
    {
        if (disposed) return;

        elapsed += Math.Max(0f, dt);
        RefreshWeapons();
        UpdateSimulatedWeapons();
        DrainPendingStates();
        SendOwnStates(dt);
    }

    // A hull swap rebinds the ship to a fresh hull, and an authority move flips who simulates, so both restart the stream.
    // A hull without a weapon is looked up again, in case it registered before its weapon was found. Only Tick refreshes, right
    // before the simulation claim, so a stream's IsOwn always matches the weapons this client simulates.
    private void RefreshWeapons()
    {
        foreach (var shipId in streams.Keys.ToArray())
        {
            if (!Registry.TryGet(shipId, out _)) streams.Remove(shipId);
        }

        foreach (var ship in Registry.Ships)
        {
            bool isOwn = IsSimulatedHere(ship);
            if (!streams.TryGetValue(ship.ShipId, out var stream) || stream.Hull != ship.Hull || stream.IsOwn != isOwn)
            {
                stream = new WeaponStream(ship.Hull, isOwn, stream?.SentRevision ?? 0);
                streams[ship.ShipId] = stream;
            }

            if (stream.Weapon == null && engine.TryGetWeapon(ship.Hull, out var weapon, out var key))
            {
                stream.Weapon = weapon;
                stream.Key = key;
            }
        }
    }

    private bool IsSimulatedHere(NetworkShipInfo ship) =>
        ship.CurrentAuthority == session.OwnControllerId && !ships.IsReplacingHull(ship.ShipId);

    private void UpdateSimulatedWeapons()
    {
        var own = streams.Values.Where(stream => stream.IsOwn && stream.Weapon != null).Select(stream => stream.Weapon).ToList();
        if (simulatedWeapons != null && own.Count == simulatedWeapons.Count && !own.Except(simulatedWeapons).Any()) return;

        simulatedWeapons = own;
        engine.SetSimulatedWeapons(own);
        Logger.Information("[NavalSync] This client now simulates {Count} ship weapon(s)", own.Count);
    }

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
            if (state == null || !state.DiffersFrom(stream.Sent)) continue;

            stream.Sent = state;
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

    private void Handle_NetworkShipWeaponShot(MessagePayload<NetworkShipWeaponShot> payload)
    {
        var shot = payload.What;
        GameThread.RunSafe(() =>
        {
            if (disposed || Mission.Current == null) return;
            AcceptShot(shot);
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

        var rejection = ValidateShot(ship, session.OwnControllerId, shot);
        if (rejection == null && ship.IsNpcParty && hostEpochPolicy.IsStale(shot.HostEpoch, session.HostEpoch))
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
    internal static string ValidateShot(NetworkShipInfo ship, string ownControllerId, NetworkShipWeaponShot shot)
    {
        var rejection = AuthorityStamp.ValidateSender(shot.OwnerControllerId, ship.CurrentAuthority, ownControllerId);
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

    private void DropShot(string reason)
    {
        shotsDropped++;
        lastDrop = "shot_" + reason;
    }

    private void Handle_NetworkShipWeaponState(MessagePayload<NetworkShipWeaponState> payload)
    {
        var state = payload.What;
        GameThread.RunSafe(() =>
        {
            if (disposed || Mission.Current == null) return;
            AcceptState(state);
        }, context: nameof(Handle_NetworkShipWeaponState));
    }

    /// <summary>[Game thread] Sets an owner's weapon state on its copy; one for a hull not spawned here yet waits for it.</summary>
    internal void AcceptState(NetworkShipWeaponState state)
    {
        if (state == null) return;
        if (!Registry.TryGet(state.ShipId, out var ship) || !streams.TryGetValue(state.ShipId, out var stream))
        {
            if (Registry.StateOf(state.ShipId) == ShipLifecycle.SpawnFailed)
            {
                DropState("spawn_failed");
                return;
            }
            if (!pendingStates.TryGetValue(state.ShipId, out var pending) || state.Stamp.IsNewer(pending.Stamp))
                pendingStates[state.ShipId] = state;
            return;
        }

        var rejection = ValidateState(ship, session.OwnControllerId, state, stream.AppliedRevision, stream.AppliedEpoch);
        if (rejection == null && ship.IsNpcParty && hostEpochPolicy.IsStale(state.HostEpoch, session.HostEpoch))
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
        if (ship.IsNpcParty) stream.AppliedEpoch = state.HostEpoch;
        statesApplied++;
    }

    private void DropState(string reason)
    {
        statesDropped++;
        lastDrop = "state_" + reason;
    }

    /// <summary>Why a weapon state must be dropped, or null to apply it.</summary>
    internal static string ValidateState(NetworkShipInfo ship, string ownControllerId, NetworkShipWeaponState state,
        long appliedRevision, int appliedEpoch)
    {
        // A new host numbers an AI hull's weapon states on from its own count, so a newer epoch restarts the order.
        var rejection = state.Stamp.Validate(ship.CurrentAuthority, ownControllerId,
            new AuthorityStamp(ship.CurrentAuthority, appliedEpoch, appliedRevision), followsHostEpoch: ship.IsNpcParty);
        if (rejection != null) return rejection;
        if (state.State == null || !state.State.IsValid) return "invalid_state";
        return null;
    }

    private void DrainPendingStates()
    {
        if (pendingStates.Count == 0) return;

        foreach (var shipId in pendingStates.Keys.ToArray())
        {
            // The ship replicator gave up on a hull that could not spawn, so its state never applies.
            if (Registry.StateOf(shipId) == ShipLifecycle.SpawnFailed)
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

    private sealed class WeaponStream
    {
        public WeaponStream(MissionObject hull, bool isOwn, long sentRevision)
        {
            Hull = hull;
            IsOwn = isOwn;
            SentRevision = sentRevision;
        }

        public MissionObject Hull { get; }
        /// <summary>Whether this client simulates the weapon: the hull is its own and not mid-swap.</summary>
        public bool IsOwn { get; }
        public MissionObject Weapon { get; set; }
        public string Key { get; set; }
        /// <summary>The owner's last sent state and its revision, which carries over a restart so peers keep accepting it.</summary>
        public BattleShipWeaponState Sent { get; set; }
        public long SentRevision { get; set; }
        /// <summary>A copy's last applied state, its revision and its host epoch.</summary>
        public BattleShipWeaponState Applied { get; set; }
        public long AppliedRevision { get; set; }
        public int AppliedEpoch { get; set; }
        /// <summary>This client's replicator clock at the last replayed shot.</summary>
        public float LastReplayedAt { get; set; }
        public bool HasReplayed { get; set; }
    }
}
