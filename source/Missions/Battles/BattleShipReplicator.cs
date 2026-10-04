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
/// the first deployment commit and stream their frame at 20 Hz; other owners' hulls are spawned as kinematic
/// copies and follow those frames.
/// </summary>
public interface IBattleShipReplicator : IDisposable
{
    /// <summary>[Game thread] Announce, stream and interpolate hulls for this frame.</summary>
    void Tick(float dt);

    /// <summary>[Game thread] Registry and stream state per hull (diagnostics).</summary>
    object Inspect();
}

/// <inheritdoc cref="IBattleShipReplicator"/>
public class BattleShipReplicator : IBattleShipReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleShipReplicator>();

    internal const float SendIntervalSeconds = 0.05f;
    internal const float InterpolationSeconds = 0.05f;
    internal static readonly long SampleLifetimeTicks = TimeSpan.FromSeconds(1).Ticks;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly ICoopMissionComponent missionComponent;
    private readonly INavalShipEngine engine;
    private readonly IBattleTeamResolver teamResolver;
    private readonly IObjectManager objectManager;

    private readonly Dictionary<Guid, ShipStream> streams = new Dictionary<Guid, ShipStream>();
    private readonly List<BattleShipSpawnData> pendingForeignHulls = new List<BattleShipSpawnData>();
    private bool spawningForeignHull;
    private bool spawnRecordsSent;
    private float sendElapsed;

    private INetworkShipRegistry Registry => missionComponent.ShipRegistry;

    public BattleShipReplicator(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IBattleSession session,
        IBattleDeploymentCoordinator deployment,
        ICoopMissionComponent missionComponent,
        INavalShipEngine engine,
        IBattleTeamResolver teamResolver,
        IObjectManager objectManager)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.deployment = deployment;
        this.missionComponent = missionComponent;
        this.engine = engine;
        this.teamResolver = teamResolver;
        this.objectManager = objectManager;

        missionComponent.AgentMovementHandler.ConfigureShipCrewMovement(IsCrewOfStreamedForeignHull);
        messageBroker.Subscribe<ShipSpawnedInBattle>(Handle_ShipSpawned);
        messageBroker.Subscribe<NetworkSpawnBattleShips>(Handle_NetworkSpawnBattleShips);
        messageBroker.Subscribe<NetworkBattleShipSample>(Handle_NetworkBattleShipSample);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
    }

    public void Dispose()
    {
        missionComponent.AgentMovementHandler.ConfigureShipCrewMovement(null);
        messageBroker.Unsubscribe<ShipSpawnedInBattle>(Handle_ShipSpawned);
        messageBroker.Unsubscribe<NetworkSpawnBattleShips>(Handle_NetworkSpawnBattleShips);
        messageBroker.Unsubscribe<NetworkBattleShipSample>(Handle_NetworkBattleShipSample);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
    }

    // [Game thread] Published synchronously by the spawn postfix. Hulls this client fields go on PlayerTeam; the
    // copies this replicator spawns raise the same event and are registered by their spawn path instead.
    private void Handle_ShipSpawned(MessagePayload<ShipSpawnedInBattle> payload)
    {
        var hull = payload.What.Hull;
        var formation = payload.What.Formation;
        if (spawningForeignHull || hull == null || formation?.Team == null || formation.Team != Mission.Current?.PlayerTeam)
            return;

        var ship = new NetworkShipInfo(Guid.NewGuid(), session.OwnControllerId, GetOwnMapEventPartyId(), false, hull, formation);
        if (Registry.TryRegister(ship))
            Logger.Information("[NavalSync] Registered own hull {ShipId} on formation {Formation}", ship.ShipId, formation.FormationIndex);
    }

    public void Tick(float dt)
    {
        PruneRemovedHulls();
        DrainPendingForeignHulls();

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

        long deadline = DateTime.UtcNow.Ticks + SampleLifetimeTicks;
        foreach (var ship in Registry.Ships.Where(IsOwnHull))
        {
            var stream = GetStream(ship.ShipId);
            stream.Sent++;
            network.SendAll(new NetworkBattleShipSample(ship.ShipId, session.OwnControllerId, stream.Sent, deadline,
                NetworkBattleShipSample.FromFrame(engine.GetFrame(ship.Hull))));
        }
    }

    private void Handle_NetworkSpawnBattleShips(MessagePayload<NetworkSpawnBattleShips> payload)
    {
        var records = payload.What.Ships ?? Array.Empty<BattleShipSpawnData>();
        GameThread.RunSafe(() =>
        {
            pendingForeignHulls.AddRange(records.Where(record => record != null));
            DrainPendingForeignHulls();
        }, context: nameof(Handle_NetworkSpawnBattleShips));
    }

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

        var team = teamResolver.ResolveReplicatedTeam(data.Side, isOwn: false);
        if (team == null) return false;

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

        if (hull == null)
        {
            Logger.Error("[NavalSync] Could not spawn hull {ShipId} ({Hull}) of {Owner} on team {Team}",
                data.ShipId, data.HullId, data.OwnerControllerId, team.TeamSide);
            return true;
        }

        Registry.TryRegister(new NetworkShipInfo(data.ShipId, data.OwnerControllerId, data.MapEventPartyId, data.IsNpcParty, hull, formation));
        Logger.Information("[NavalSync] Spawned foreign hull {ShipId} ({Hull}) of {Owner} on team {Team} formation {Formation}",
            data.ShipId, data.HullId, data.OwnerControllerId, team.TeamSide, formation?.FormationIndex);
        return true;
    }

    private void Handle_NetworkBattleShipSample(MessagePayload<NetworkBattleShipSample> payload)
    {
        var sample = payload.What;
        GameThread.RunSafe(() => AcceptSample(sample), context: nameof(Handle_NetworkBattleShipSample));
    }

    private void AcceptSample(NetworkBattleShipSample sample)
    {
        if (Mission.Current == null || sample == null) return;

        var stream = GetStream(sample.ShipId);
        if (!Registry.TryGet(sample.ShipId, out var ship))
        {
            stream.Reject("unknown_ship");
            return;
        }

        var rejection = ValidateSample(ship, session.OwnControllerId, sample, stream.Accepted, DateTime.UtcNow.Ticks);
        if (rejection != null)
        {
            stream.Reject(rejection);
            return;
        }

        stream.Start = stream.HasWritten ? stream.Written : engine.GetFrame(ship.Hull);
        stream.Target = sample;
        stream.Accepted = sample.Sequence;
        stream.Elapsed = 0;
    }

    /// <summary>Why a sample must be dropped, or null to accept it.</summary>
    internal static string ValidateSample(NetworkShipInfo ship, string ownControllerId, NetworkBattleShipSample sample,
        long acceptedSequence, long nowUtcTicks)
    {
        if (ship.CurrentAuthority == ownControllerId) return "own_hull";
        if (sample.OwnerControllerId != ship.CurrentAuthority) return "not_authority";
        if (sample.Sequence <= acceptedSequence) return "stale";
        if (sample.DeadlineUtcTicks <= nowUtcTicks || sample.DeadlineUtcTicks > nowUtcTicks + SampleLifetimeTicks) return "expired";
        if (!sample.HasValidFrame) return "invalid_frame";
        return null;
    }

    private void ApplyForeignFrames(float dt)
    {
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;

        long now = DateTime.UtcNow.Ticks;
        foreach (var entry in streams)
        {
            var stream = entry.Value;
            var target = stream.Target;
            if (target == null || !Registry.TryGet(entry.Key, out var ship)) continue;

            if (target.DeadlineUtcTicks <= now)
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

    // Crew poses are world-space; while a foreign hull streams, its crew rides the deck instead.
    private bool IsCrewOfStreamedForeignHull(Agent agent)
    {
        return agent?.Formation != null
            && Registry.TryGetByFormation(agent.Formation, out var ship)
            && !IsOwnHull(ship)
            && streams.TryGetValue(ship.ShipId, out var stream)
            && stream.Target != null;
    }

    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        var entered = payload.What;
        if (entered.InstanceId != null && entered.InstanceId != session.InstanceId) return;

        GameThread.RunSafe(() =>
        {
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
        ships = Registry.Ships.Select(ship =>
        {
            streams.TryGetValue(ship.ShipId, out var stream);
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
                sent = stream?.Sent ?? 0,
                accepted = stream?.Accepted ?? 0,
                applied = stream?.Applied ?? 0,
                rejects = stream?.Rejects ?? 0,
                lastReject = stream?.LastReject,
                streaming = stream?.Target != null,
            };
        }).ToArray(),
    };

    private sealed class ShipStream
    {
        public NetworkBattleShipSample Target;
        public MatrixFrame Start;
        public MatrixFrame Written;
        public bool HasWritten;
        public float Elapsed;
        public long Sent;
        public long Accepted;
        public long Applied;
        public long Rejects;
        public string LastReject;

        public void Reject(string reason)
        {
            Rejects++;
            LastReject = reason;
        }
    }
}
