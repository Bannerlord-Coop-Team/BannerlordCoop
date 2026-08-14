using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Messages;
using Missions.Missiles.Handlers;
using Missions.Missiles.Message;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Routes puppet damage to the client that owns the victim.</summary>
public interface IBattleDamageRouter : IDisposable
{
    void Tick(float dt);
    void FlushForMissionEnd();
#if DEBUG
    BattleDamageRouter.RoutedDamageDebugSnapshot GetRoutedDamageDebugSnapshot(
        Guid routedHitId);
    BattleDamageRouter.RoutedDamageDebugSnapshot GetIncomingAiDamageSourceDebugSnapshot(
        Guid victimRiderAgentId,
        string attackerControllerId,
        string victimControllerId,
        long afterSequence);
#endif
}

/// <inheritdoc cref="IBattleDamageRouter"/>
public class BattleDamageRouter : IBattleDamageRouter
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleDamageRouter>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly IGuardedHitWindow guardedHitWindow;
    private readonly IAgentNativeMountState agentNativeMountState;
    private readonly Func<Agent, bool?> mountAuthorityProbe;
    private readonly object inboundDamageGate = new();
    private readonly ConcurrentQueue<NetworkApplyBattleDamage> inboundDamage = new();
    private readonly Queue<PendingLocalDamage> pendingLocalDamage = new();
    private readonly Queue<DeferredDamage> deferredDamage = new();
    private readonly Dictionary<(Guid AgentId, long ShotSequence), ReconstructionInfo> reconstructions = new();
    private readonly Queue<(Guid AgentId, long ShotSequence)> reconstructionHistory = new();
    private readonly Dictionary<Guid, NoHealthReductionWarningState> noHealthReductionWarnings = new();
    private long presentationEpoch;
    private float presentationTime;
    private bool disposed;
    private bool closing;

#if DEBUG
    private readonly Dictionary<Guid, RoutedDamageDebugRecord>
        routedDamageDebugRecords = new Dictionary<Guid, RoutedDamageDebugRecord>();
    private readonly Queue<Guid> routedDamageDebugHistory = new Queue<Guid>();
    private long routedDamageDebugSequence;
    private const int RoutedDamageDebugHistoryLimit = 2048;

    public sealed class RoutedDamageDebugVector
    {
        public RoutedDamageDebugVector(Vec3 value)
        {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    public sealed class RoutedDamageDebugSnapshot
    {
        public long Sequence { get; set; }
        public Guid RoutedHitId { get; set; }
        public Guid AttackerAgentId { get; set; }
        public bool TargetIsMount { get; set; }
        public Guid RiderAgentId { get; set; }
        public Guid MountAgentId { get; set; }
        public Guid VictimAgentId { get; set; }
        public Guid ActualVictimAgentId { get; set; }
        public string AttackerControllerId { get; set; }
        public string VictimControllerId { get; set; }
        public bool AttackerIsAi { get; set; }
        public bool ContactTelemetryAvailable { get; set; }
        public int AttackerIndex { get; set; }
        public int VictimIndex { get; set; }
        public int RoutedDamage { get; set; }
        public int InputDamage { get; set; }
        public float AppliedDamage { get; set; }
        public float HealthBefore { get; set; }
        public float HealthAfter { get; set; }
        public string CollisionResult { get; set; }
        public string VictimHitBodyPart { get; set; }
        public RoutedDamageDebugVector CollisionPosition { get; set; }
        public RoutedDamageDebugVector BlowDirection { get; set; }
        public float AttackProgress { get; set; }
        public float MountedSpeed { get; set; }
        public float TargetDrift { get; set; }
        public float TargetAge { get; set; }
        public bool NetworkApplySent { get; set; }
        public bool OwnerApplied { get; set; }
    }

    private sealed class RoutedDamageDebugRecord
    {
        public RoutedDamageDebugSnapshot Snapshot { get; set; }
    }
#endif

    private const int MinimumPresentationEpochs = 2;
    private const int MaxReconstructionHistory = 4096;
    private const double DamageTimeoutSeconds = 4d;
    private const float UnknownShotGraceSeconds = 0.5f;
    private const float MinimumFlightSeconds = 0.05f;
    private const float MaximumFlightSeconds = 2.5f;
    private const float MaximumTickSeconds = 0.1f;
    private const double NoHealthReductionWarningIntervalSeconds = 5d;

    private sealed class NoHealthReductionWarningState
    {
        public long LastWarningTimestamp { get; set; }
        public int SuppressedHits { get; set; }
    }

    private readonly struct ReconstructionInfo
    {
        public Vec3 Position { get; }
        public float Speed { get; }
        public float RemainingFlightSeconds { get; }
        public long Epoch { get; }
        public float Time { get; }

        public ReconstructionInfo(MissileReconstructed missile, long epoch, float time)
        {
            Position = missile.Position;
            Speed = missile.Speed;
            RemainingFlightSeconds = missile.RemainingFlightSeconds;
            Epoch = epoch;
            Time = time;
        }
    }

    private readonly struct DeferredDamage
    {
        public NetworkApplyBattleDamage Damage { get; }
        public bool NeedsPresentation { get; }
        public long EarliestEpoch { get; }
        public float FallbackDeadline { get; }
        public long EnqueuedTimestamp { get; }

        public DeferredDamage(NetworkApplyBattleDamage damage, bool needsPresentation,
            long earliestEpoch, float fallbackDeadline)
        {
            Damage = damage;
            NeedsPresentation = needsPresentation;
            EarliestEpoch = earliestEpoch;
            FallbackDeadline = fallbackDeadline;
            EnqueuedTimestamp = Stopwatch.GetTimestamp();
        }
    }

    private sealed class PendingLocalDamage
    {
        public BattlePuppetHit Hit { get; }
        public Guid AttackerId { get; }
        public long ShotSequence { get; }
        public WeaponComponentData AttackerWeapon { get; }
        public long ReadyEpoch { get; }
        public long GuardCandidateId { get; }

        public PendingLocalDamage(
            BattlePuppetHit hit,
            Guid attackerId,
            long shotSequence,
            WeaponComponentData attackerWeapon,
            long readyEpoch,
            long guardCandidateId)
        {
            Hit = hit;
            AttackerId = attackerId;
            ShotSequence = shotSequence;
            AttackerWeapon = attackerWeapon;
            ReadyEpoch = readyEpoch;
            GuardCandidateId = guardCandidateId;
        }
    }

    public BattleDamageRouter(IBattleNetwork network, IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent, IBattleSession session,
        IGuardedHitWindow guardedHitWindow,
        IAgentNativeMountState agentNativeMountState)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.guardedHitWindow = guardedHitWindow;
        this.agentNativeMountState = agentNativeMountState;

        messageBroker.Subscribe<BattlePuppetHit>(Handle_BattlePuppetHit);
        messageBroker.Subscribe<NetworkApplyBattleDamage>(Handle_NetworkApplyBattleDamage);
        messageBroker.Subscribe<MissileReconstructed>(Handle_MissileReconstructed);
        mountAuthorityProbe = ProbeMountAuthority;
        BattleSpawnGate.MountAuthorityProbe = mountAuthorityProbe;
    }

    public void Dispose()
    {
        lock (inboundDamageGate)
        {
            if (disposed)
                return;
            disposed = true;
            closing = true;
        }

        messageBroker.Unsubscribe<BattlePuppetHit>(Handle_BattlePuppetHit);
        messageBroker.Unsubscribe<NetworkApplyBattleDamage>(Handle_NetworkApplyBattleDamage);
        messageBroker.Unsubscribe<MissileReconstructed>(Handle_MissileReconstructed);
        deferredDamage.Clear();
        pendingLocalDamage.Clear();
        reconstructions.Clear();
        reconstructionHistory.Clear();
        noHealthReductionWarnings.Clear();
#if DEBUG
        routedDamageDebugRecords.Clear();
        routedDamageDebugHistory.Clear();
#endif
        while (inboundDamage.TryDequeue(out _)) { }

        if (BattleSpawnGate.MountAuthorityProbe == mountAuthorityProbe)
            BattleSpawnGate.MountAuthorityProbe = null;

        guardedHitWindow.Dispose();
    }

    private bool? ProbeMountAuthority(Agent mount)
    {
        if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(mount, out var info))
            return null;
        return info.CurrentAuthority != session.OwnControllerId;
    }

    private void Handle_MissileReconstructed(MessagePayload<MissileReconstructed> payload)
    {
        MissileReconstructed missile = payload.What;
        if (disposed || closing || missile.AgentId == Guid.Empty || missile.ShotSequence == 0)
            return;

        var key = (missile.AgentId, missile.ShotSequence);
        reconstructions[key] = new ReconstructionInfo(missile, presentationEpoch, presentationTime);
        reconstructionHistory.Enqueue(key);
        while (reconstructionHistory.Count > MaxReconstructionHistory)
            reconstructions.Remove(reconstructionHistory.Dequeue());
    }

    public void Tick(float dt)
    {
        if (disposed || closing)
            return;

        guardedHitWindow.Advance();
        DrainPendingLocalDamage();
        presentationEpoch++;
        if (!float.IsNaN(dt) && !float.IsInfinity(dt) && dt > 0f)
            presentationTime += Math.Min(dt, MaximumTickSeconds);

        DrainInboundDamage();
        int count = deferredDamage.Count;
        var blockedVictims = new HashSet<Guid>();
        for (int i = 0; i < count; i++)
        {
            DeferredDamage deferred = deferredDamage.Dequeue();
            Guid victimId = deferred.Damage.VictimAgentId;
            if (blockedVictims.Contains(victimId) || IsWaiting(deferred))
            {
                deferredDamage.Enqueue(deferred);
                blockedVictims.Add(victimId);
            }
            else
            {
                ApplyDeferredDamage(deferred.Damage);
            }
        }
    }

    public void FlushForMissionEnd()
    {
        lock (inboundDamageGate)
        {
            if (disposed || closing)
                return;
            closing = true;
        }

        DrainPendingLocalDamage(force: true);
        while (deferredDamage.Count > 0)
            ApplyDeferredDamage(deferredDamage.Dequeue().Damage);
        while (inboundDamage.TryDequeue(out NetworkApplyBattleDamage damage))
            TryApplyNetworkDamage(damage, authorityWasVerified: false);
    }

    private void Handle_BattlePuppetHit(MessagePayload<BattlePuppetHit> payload)
    {
        if (disposed || closing)
            return;

#if DEBUG
        RecordMountedContactTelemetryForActors(
            payload.What.Attacker,
            payload.What.Victim);
#endif

        var registry = coopMissionComponent.AgentRegistry;
        Guid attackerId = Guid.Empty;
        if (payload.What.Attacker != null
            && registry.TryGetAgentInfo(payload.What.Attacker, out var attackerInfo))
        {
            attackerId = attackerInfo.AgentId;
        }

        long shotSequence = 0;
        WeaponComponentData attackerWeapon = null;
        if (payload.What.Blow.IsMissile)
        {
            int missileIndex = payload.What.Blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex;
            if (Mission.Current?._missilesDictionary.TryGetValue(missileIndex, out var missile) == true)
                attackerWeapon = missile.Weapon.CurrentUsageItem;
            else
                Logger.Error("Failed to resolve routed missile weapon at source index {MissileIndex}", missileIndex);

            if (coopMissionComponent.MissileHandler.TryTakeLocalShot(missileIndex,
                out Guid shotAgentId, out shotSequence))
            {
                if (attackerId != Guid.Empty && attackerId != shotAgentId)
                    shotSequence = 0;
                else
                    attackerId = shotAgentId;
            }
            else
            {
                Logger.Warning("Could not correlate local missile hit at source index {MissileIndex}",
                    missileIndex);
            }
        }

        Blow candidateBlow = payload.What.Blow;
        AttackCollisionData candidateCollision =
            payload.What.CollisionData;
        long guardCandidateId = payload.What.IsMount
            ? 0
            : guardedHitWindow.RegisterCandidate(
                payload.What.Victim,
                payload.What.Attacker,
                in candidateBlow,
                in candidateCollision);
        var pending = new PendingLocalDamage(
            payload.What,
            attackerId,
            shotSequence,
            attackerWeapon,
            guardedHitWindow.Epoch + 1,
            guardCandidateId);

        if (payload.What.IsMount)
        {
            RouteLocalDamage(pending);
            return;
        }

        pendingLocalDamage.Enqueue(pending);
    }

#if DEBUG
    private void RecordMountedContactTelemetryForActors(
        Agent attacker,
        Agent victim)
    {
        IAgentMovementHandler movementHandler =
            coopMissionComponent.AgentMovementHandler;
        if (!(movementHandler is IAgentMovementDebugControl debugControl))
        {
            return;
        }

        bool hasAttacker = TryGetMountedContactTelemetry(
            movementHandler.Interpolator,
            attacker,
            out float attackerDrift,
            out float attackerTargetAge);
        float victimDrift = 0f;
        float victimTargetAge = 0f;
        bool hasVictim = !ReferenceEquals(attacker, victim) &&
            TryGetMountedContactTelemetry(
                movementHandler.Interpolator,
                victim,
                out victimDrift,
                out victimTargetAge);
        if (!hasAttacker && !hasVictim)
            return;

        debugControl.RecordMountedContact(
            Math.Max(attackerDrift, victimDrift),
            Math.Max(attackerTargetAge, victimTargetAge));
    }

    private static bool TryGetMountedContactTelemetry(
        IAgentPositionInterpolator interpolator,
        Agent agent,
        out float drift,
        out float targetAge)
    {
        drift = 0f;
        targetAge = 0f;
        return interpolator.TryGetMountedTargetTelemetry(
            agent,
            out drift,
            out targetAge);
    }
#endif

    private void DrainPendingLocalDamage(bool force = false)
    {
        int count = pendingLocalDamage.Count;
        for (int i = 0; i < count; i++)
        {
            PendingLocalDamage pending = pendingLocalDamage.Dequeue();
            if (!force &&
                pending.ReadyEpoch > guardedHitWindow.Epoch)
            {
                pendingLocalDamage.Enqueue(pending);
                continue;
            }

            if (guardedHitWindow.CompleteCandidate(
                    pending.GuardCandidateId))
            {
                continue;
            }

            RouteLocalDamage(pending);
        }
    }

    private void RouteLocalDamage(PendingLocalDamage pending)
    {
        var registry = coopMissionComponent.AgentRegistry;
        BattlePuppetHit hit = pending.Hit;
        if (registry.TryGetAgentInfo(hit.Victim, out var victimInfo))
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug(
                    "[BattleDamage] Routing blow: victimId={VictimId} victimAuthority={VictimAuthority} " +
                    "victimIndex={VictimIndex} attackerId={AttackerId} sourceController={SourceController} " +
                    "damage={Damage} victimIsMount={VictimIsMount} riderKeyedMount={RiderKeyedMount} " +
                    "missile={Missile} shotSequence={ShotSequence}",
                    victimInfo.AgentId,
                    victimInfo.CurrentAuthority,
                    hit.Victim?.Index ?? -1,
                    pending.AttackerId,
                    session.OwnControllerId,
                    hit.Blow.InflictedDamage,
                    hit.Victim?.IsMount ?? hit.IsMount,
                    false,
                    hit.Blow.IsMissile,
                    pending.ShotSequence);
            }
#if DEBUG
            Guid routedHitId = Guid.NewGuid();
#endif
            network.SendAll(new NetworkApplyBattleDamage(
                victimInfo.AgentId,
                pending.AttackerId,
                hit.Blow,
                hit.CollisionData,
                missileShotSequence: pending.ShotSequence,
                attackerWeapon: pending.AttackerWeapon
#if DEBUG
                , debugRoutedHitId: routedHitId,
                debugAttackerControllerId:
                    TryGetAgentControllerId(hit.Attacker),
                debugAttackerIsAi: hit.Attacker?.IsAIControlled == true
#endif
                ));
#if DEBUG
            RecordRoutedDamageSent(
                pending,
                routedHitId,
                hit.Victim?.IsMount ?? hit.IsMount,
                hit.Victim?.IsMount == true &&
                hit.Victim.RiderAgent is Agent registeredMountRider &&
                registry.TryGetAgentInfo(registeredMountRider, out var mountRiderInfo)
                    ? mountRiderInfo.AgentId
                    : Guid.Empty,
                hit.Victim?.IsMount == true
                    ? victimInfo.AgentId
                    : Guid.Empty,
                victimInfo.AgentId,
                victimInfo.AgentId,
                hit.Victim);
#endif
            return;
        }

        if (hit.IsMount &&
            hit.Victim?.RiderAgent is Agent rider &&
            registry.TryGetAgentInfo(rider, out var riderInfo))
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug(
                    "[BattleDamage] Routing blow: victimId={VictimId} victimAuthority={VictimAuthority} " +
                    "victimIndex={VictimIndex} attackerId={AttackerId} sourceController={SourceController} " +
                    "damage={Damage} victimIsMount={VictimIsMount} riderKeyedMount={RiderKeyedMount} " +
                    "missile={Missile} shotSequence={ShotSequence}",
                    riderInfo.AgentId,
                    riderInfo.CurrentAuthority,
                    hit.Victim?.Index ?? -1,
                    pending.AttackerId,
                    session.OwnControllerId,
                    hit.Blow.InflictedDamage,
                    hit.Victim?.IsMount ?? true,
                    true,
                    hit.Blow.IsMissile,
                    pending.ShotSequence);
            }
#if DEBUG
            Guid routedHitId = Guid.NewGuid();
#endif
            network.SendAll(new NetworkApplyBattleDamage(
                riderInfo.AgentId,
                pending.AttackerId,
                hit.Blow,
                hit.CollisionData,
                isMount: true,
                missileShotSequence: pending.ShotSequence,
                attackerWeapon: pending.AttackerWeapon
#if DEBUG
                , debugRoutedHitId: routedHitId,
                debugAttackerControllerId:
                    TryGetAgentControllerId(hit.Attacker),
                debugAttackerIsAi: hit.Attacker?.IsAIControlled == true
#endif
                ));
#if DEBUG
            Guid mountAgentId = TryGetRegisteredAgentId(hit.Victim);
            RecordRoutedDamageSent(
                pending,
                routedHitId,
                true,
                riderInfo.AgentId,
                mountAgentId,
                riderInfo.AgentId,
                mountAgentId,
                hit.Victim);
#endif
            return;
        }

        Logger.Warning(
            "[BattleDamage] Local hit on an unregistered puppet could not be routed: victimIndex={VictimIndex} " +
            "victimName={VictimName} attackerId={AttackerId} sourceController={SourceController} " +
            "damage={Damage} victimIsMount={VictimIsMount} missile={Missile}",
            hit.Victim?.Index ?? -1,
            hit.Victim?.Name,
            pending.AttackerId,
            session.OwnControllerId,
            hit.Blow.InflictedDamage,
            hit.Victim?.IsMount ?? hit.IsMount,
            hit.Blow.IsMissile);
    }

#if DEBUG
    public RoutedDamageDebugSnapshot GetRoutedDamageDebugSnapshot(
        Guid routedHitId)
    {
        if (routedDamageDebugRecords.TryGetValue(
                routedHitId,
                out RoutedDamageDebugRecord record))
        {
            return record.Snapshot;
        }

        RoutedDamageDebugSnapshot latest = null;
        foreach (var candidate in routedDamageDebugRecords.Values)
        {
            if ((routedHitId == Guid.Empty ||
                 candidate.Snapshot.AttackerAgentId == routedHitId) &&
                (latest == null ||
                 candidate.Snapshot.Sequence > latest.Sequence))
            {
                latest = candidate.Snapshot;
            }
        }

        return latest;
    }

    public RoutedDamageDebugSnapshot GetIncomingAiDamageSourceDebugSnapshot(
        Guid victimRiderAgentId,
        string attackerControllerId,
        string victimControllerId,
        long afterSequence)
    {
        if (victimRiderAgentId == Guid.Empty ||
            string.IsNullOrEmpty(attackerControllerId) ||
            string.IsNullOrEmpty(victimControllerId))
        {
            return null;
        }

        RoutedDamageDebugSnapshot latest = null;
        foreach (var candidate in routedDamageDebugRecords.Values)
        {
            RoutedDamageDebugSnapshot snapshot = candidate.Snapshot;
            if (snapshot.Sequence > afterSequence &&
                snapshot.AttackerIsAi &&
                !snapshot.OwnerApplied &&
                snapshot.ContactTelemetryAvailable &&
                snapshot.AttackerControllerId == attackerControllerId &&
                snapshot.VictimControllerId == victimControllerId &&
                (snapshot.VictimAgentId == victimRiderAgentId ||
                 snapshot.RiderAgentId == victimRiderAgentId) &&
                (latest == null || snapshot.Sequence > latest.Sequence))
            {
                latest = snapshot;
            }
        }

        return latest;
    }

    private void RecordRoutedDamageSent(
        PendingLocalDamage pending,
        Guid routedHitId,
        bool targetIsMount,
        Guid riderAgentId,
        Guid mountAgentId,
        Guid victimAgentId,
        Guid actualVictimAgentId,
        Agent nativeVictim)
    {
        Guid attackerAgentId = pending.AttackerId;
        if (attackerAgentId == Guid.Empty)
            return;

        Agent nativeAttacker = pending.Hit.Attacker;
        float targetDrift = 0f;
        float targetAge = 0f;
        bool contactTelemetryAvailable = TryGetMountedContactTelemetry(
            coopMissionComponent.AgentMovementHandler.Interpolator,
            nativeVictim,
            out targetDrift,
            out targetAge);
        var snapshot = new RoutedDamageDebugSnapshot
        {
            Sequence = ++routedDamageDebugSequence,
            RoutedHitId = routedHitId,
            AttackerAgentId = attackerAgentId,
            TargetIsMount = targetIsMount,
            RiderAgentId = riderAgentId,
            MountAgentId = mountAgentId,
            VictimAgentId = victimAgentId,
            ActualVictimAgentId = actualVictimAgentId,
            AttackerControllerId = TryGetAgentControllerId(nativeAttacker),
            VictimControllerId = TryGetAgentControllerId(nativeVictim),
            AttackerIsAi = nativeAttacker?.IsAIControlled == true,
            ContactTelemetryAvailable = contactTelemetryAvailable,
            AttackerIndex = nativeAttacker?.Index ?? -1,
            VictimIndex = nativeVictim?.Index ?? -1,
            RoutedDamage = pending.Hit.Blow.InflictedDamage,
            InputDamage = pending.Hit.Blow.InflictedDamage,
            AppliedDamage = 0f,
            HealthBefore = nativeVictim?.Health ?? -1f,
            HealthAfter = nativeVictim?.Health ?? -1f,
            CollisionResult =
                pending.Hit.CollisionData.CollisionResult.ToString(),
            VictimHitBodyPart =
                pending.Hit.CollisionData.VictimHitBodyPart.ToString(),
            CollisionPosition = new RoutedDamageDebugVector(
                pending.Hit.CollisionData.CollisionGlobalPosition),
            BlowDirection = new RoutedDamageDebugVector(
                pending.Hit.Blow.Direction),
            AttackProgress = pending.Hit.CollisionData.AttackProgress,
            MountedSpeed = nativeAttacker?.MountAgent?
                .GetRealGlobalVelocity().AsVec2.Length ?? 0f,
            TargetDrift = targetDrift,
            TargetAge = targetAge,
            NetworkApplySent = true,
            OwnerApplied = false,
        };
        StoreRoutedDamageDebugSnapshot(snapshot);
    }

    private void RecordRoutedDamageApplied(
        NetworkApplyBattleDamage damage,
        Agent attacker,
        Agent victim,
        int routedDamage,
        int inputDamage,
        float appliedDamage,
        float healthBefore,
        float healthAfter)
    {
        if (damage.AttackerAgentId == Guid.Empty ||
            damage.DebugRoutedHitId == Guid.Empty)
            return;

        float targetDrift = 0f;
        float targetAge = 0f;
        bool contactTelemetryAvailable = TryGetMountedContactTelemetry(
            coopMissionComponent.AgentMovementHandler.Interpolator,
            victim,
            out targetDrift,
            out targetAge);
        var snapshot = new RoutedDamageDebugSnapshot
        {
            Sequence = ++routedDamageDebugSequence,
            RoutedHitId = damage.DebugRoutedHitId,
            AttackerAgentId = damage.AttackerAgentId,
            TargetIsMount = victim.IsMount,
            RiderAgentId = damage.IsMount
                ? damage.VictimAgentId
                : TryGetRegisteredAgentId(victim.RiderAgent),
            MountAgentId = victim.IsMount
                ? TryGetRegisteredAgentId(victim)
                : Guid.Empty,
            VictimAgentId = damage.VictimAgentId,
            ActualVictimAgentId = victim.IsMount
                ? TryGetRegisteredAgentId(victim)
                : damage.VictimAgentId,
            AttackerControllerId = TryGetAgentControllerId(attacker) ??
                TryGetControllerId(damage.AttackerAgentId) ??
                damage.DebugAttackerControllerId,
            VictimControllerId = TryGetAgentControllerId(victim) ??
                TryGetControllerId(damage.VictimAgentId),
            AttackerIsAi = attacker?.IsAIControlled == true ||
                damage.DebugAttackerIsAi,
            ContactTelemetryAvailable = contactTelemetryAvailable,
            AttackerIndex = attacker?.Index ?? -1,
            VictimIndex = victim?.Index ?? -1,
            RoutedDamage = routedDamage,
            InputDamage = inputDamage,
            AppliedDamage = appliedDamage,
            HealthBefore = healthBefore,
            HealthAfter = healthAfter,
            CollisionResult = damage.CollisionData.CollisionResult.ToString(),
            VictimHitBodyPart =
                damage.CollisionData.VictimHitBodyPart.ToString(),
            CollisionPosition = new RoutedDamageDebugVector(
                damage.CollisionData.CollisionGlobalPosition),
            BlowDirection = new RoutedDamageDebugVector(
                damage.Blow.Direction),
            AttackProgress = damage.CollisionData.AttackProgress,
            MountedSpeed = attacker?.MountAgent?
                .GetRealGlobalVelocity().AsVec2.Length ?? 0f,
            TargetDrift = targetDrift,
            TargetAge = targetAge,
            NetworkApplySent = true,
            OwnerApplied = true,
        };
        StoreRoutedDamageDebugSnapshot(snapshot);
    }

    private Guid TryGetRegisteredAgentId(Agent agent) =>
        agent != null && coopMissionComponent.AgentRegistry.TryGetAgentInfo(
            agent,
            out var info)
                ? info.AgentId
                : Guid.Empty;

    private string TryGetAgentControllerId(Agent agent) =>
        agent != null && coopMissionComponent.AgentRegistry.TryGetAgentInfo(
            agent,
            out var info)
                ? info.CurrentAuthority
                : null;

    private string TryGetControllerId(Guid agentId) =>
        agentId != Guid.Empty &&
        coopMissionComponent.AgentRegistry.TryGetAgentInfo(
            agentId,
            out var info)
                ? info.CurrentAuthority
                : null;

    private void StoreRoutedDamageDebugSnapshot(
        RoutedDamageDebugSnapshot snapshot)
    {
        if (!routedDamageDebugRecords.ContainsKey(snapshot.RoutedHitId))
            routedDamageDebugHistory.Enqueue(snapshot.RoutedHitId);
        routedDamageDebugRecords[snapshot.RoutedHitId] =
            new RoutedDamageDebugRecord { Snapshot = snapshot };

        while (routedDamageDebugHistory.Count >
               RoutedDamageDebugHistoryLimit)
        {
            routedDamageDebugRecords.Remove(
                routedDamageDebugHistory.Dequeue());
        }
    }
#endif

    private void Handle_NetworkApplyBattleDamage(MessagePayload<NetworkApplyBattleDamage> payload)
    {
        NetworkApplyBattleDamage damage = payload.What;
        if (IsMissileDamage(damage) && damage.MissileShotSequence != 0)
        {
            Vec3 impactVelocity = damage.Blow.WeaponRecord.Velocity;
            if (!MissileReplayPlanner.IsFinite(impactVelocity) || impactVelocity.LengthSquared <= 0.0001f)
                impactVelocity = damage.CollisionData.MissileVelocity;

            coopMissionComponent.MissileHandler.RecordImpactHint(damage.AttackerAgentId,
                damage.MissileShotSequence, damage.VictimAgentId, damage.IsMount, impactVelocity);
        }

        bool enqueued = false;
        lock (inboundDamageGate)
        {
            if (!disposed && !closing)
            {
                inboundDamage.Enqueue(damage);
                enqueued = true;
            }
        }

        if (enqueued)
            GameThread.RunSafe(DrainInboundDamage);
    }

    private void DrainInboundDamage()
    {
        while (inboundDamage.TryDequeue(out NetworkApplyBattleDamage damage))
        {
            if (!IsLocallyAuthoritativeFor(damage))
                continue;

            bool missile = IsMissileDamage(damage);
            if (missile || HasDeferredDamageFor(damage.VictimAgentId))
            {
                deferredDamage.Enqueue(new DeferredDamage(damage, missile,
                    presentationEpoch + (missile ? MinimumPresentationEpochs : 0),
                    presentationTime + (missile ? UnknownShotGraceSeconds : 0f)));
            }
            else
            {
                TryApplyNetworkDamage(damage, authorityWasVerified: true);
            }
        }
    }

    private bool HasDeferredDamageFor(Guid victimId)
    {
        foreach (DeferredDamage deferred in deferredDamage)
        {
            if (deferred.Damage.VictimAgentId == victimId)
                return true;
        }
        return false;
    }

    private bool IsWaiting(DeferredDamage deferred)
    {
        if (!deferred.NeedsPresentation)
            return false;
        if (presentationEpoch < deferred.EarliestEpoch)
            return true;
        if (ElapsedSeconds(deferred.EnqueuedTimestamp) >= DamageTimeoutSeconds)
            return false;

        NetworkApplyBattleDamage damage = deferred.Damage;
        if (damage.AttackerAgentId != Guid.Empty && damage.MissileShotSequence != 0)
        {
            if (coopMissionComponent.MissileHandler.IsReconstructionPending(
                damage.AttackerAgentId, damage.MissileShotSequence))
            {
                return true;
            }

            if (reconstructions.TryGetValue((damage.AttackerAgentId, damage.MissileShotSequence),
                out ReconstructionInfo reconstruction))
            {
                if (presentationEpoch < reconstruction.Epoch + MinimumPresentationEpochs)
                    return true;
                return presentationTime < reconstruction.Time + EstimateFlightSeconds(damage, reconstruction);
            }
        }

        return presentationTime < deferred.FallbackDeadline;
    }

    private static float EstimateFlightSeconds(NetworkApplyBattleDamage damage, ReconstructionInfo reconstruction)
    {
        if (reconstruction.RemainingFlightSeconds > 0f)
            return Math.Min(MaximumFlightSeconds, reconstruction.RemainingFlightSeconds);

        Vec3 impact = MissileReplayPlanner.IsFinite(damage.Blow.GlobalPosition)
            ? damage.Blow.GlobalPosition
            : damage.CollisionData.CollisionGlobalPosition;
        Vec3 displacement = impact - reconstruction.Position;
        if (!MissileReplayPlanner.IsFinite(displacement) || reconstruction.Speed <= 1f)
            return MinimumFlightSeconds;

        Vec3 impactVelocity = damage.Blow.WeaponRecord.Velocity;
        if (!MissileReplayPlanner.IsFinite(impactVelocity) || impactVelocity.LengthSquared <= 1f)
            impactVelocity = damage.CollisionData.MissileVelocity;
        double impactSpeed = MissileReplayPlanner.IsFinite(impactVelocity)
            ? Math.Sqrt(impactVelocity.LengthSquared)
            : 0d;
        double averageSpeed = impactSpeed > 1d
            ? (reconstruction.Speed + impactSpeed) * 0.5d
            : reconstruction.Speed;
        float flight = (float)(Math.Sqrt(displacement.LengthSquared) / averageSpeed);
        return Math.Max(MinimumFlightSeconds, Math.Min(MaximumFlightSeconds, flight));
    }

    private bool IsLocallyAuthoritativeFor(NetworkApplyBattleDamage damage)
    {
        if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(damage.VictimAgentId, out var info))
        {
            Logger.Warning(
                "[BattleDamage] Dropping routed blow at receive: victimId={VictimId} attackerId={AttackerId} " +
                "localController={LocalController} damage={Damage} riderKeyedMount={RiderKeyedMount} " +
                "missile={Missile} " +
                "reason=unregistered-victim",
                damage.VictimAgentId,
                damage.AttackerAgentId,
                session.OwnControllerId,
                damage.Blow.InflictedDamage,
                damage.IsMount,
                IsMissileDamage(damage));
            return false;
        }

        return info.CurrentAuthority == session.OwnControllerId;
    }

    private void ApplyDeferredDamage(NetworkApplyBattleDamage damage)
    {
        TryApplyNetworkDamage(damage, authorityWasVerified: true);
        if (damage.AttackerAgentId != Guid.Empty && damage.MissileShotSequence != 0)
            reconstructions.Remove((damage.AttackerAgentId, damage.MissileShotSequence));
    }

    private void TryApplyNetworkDamage(
        NetworkApplyBattleDamage damage,
        bool authorityWasVerified)
    {
        try
        {
            ApplyNetworkDamage(damage, authorityWasVerified);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply routed battle damage");
        }
    }

    private static bool IsMissileDamage(NetworkApplyBattleDamage damage) =>
        damage.IsMissile || damage.Blow.IsMissile;

    private static double ElapsedSeconds(long timestamp) =>
        (Stopwatch.GetTimestamp() - timestamp) / (double)Stopwatch.Frequency;

    private void ApplyNetworkDamage(
        NetworkApplyBattleDamage damage,
        bool authorityWasVerified)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(damage.VictimAgentId, out var info))
        {
            if (authorityWasVerified)
            {
                Logger.Warning(
                    "[BattleDamage] Dropping routed blow at apply: victimId={VictimId} attackerId={AttackerId} " +
                    "localController={LocalController} damage={Damage} reason=registry-lost-after-accept",
                    damage.VictimAgentId,
                    damage.AttackerAgentId,
                    session.OwnControllerId,
                    damage.Blow.InflictedDamage);
            }
            return;
        }
        if (info.CurrentAuthority != session.OwnControllerId)
        {
            if (authorityWasVerified)
            {
                Logger.Warning(
                    "[BattleDamage] Dropping routed blow at apply: victimId={VictimId} attackerId={AttackerId} " +
                    "localController={LocalController} currentAuthority={CurrentAuthority} damage={Damage} " +
                    "reason=authority-changed-after-accept",
                    damage.VictimAgentId,
                    damage.AttackerAgentId,
                    session.OwnControllerId,
                    info.CurrentAuthority,
                    damage.Blow.InflictedDamage);
            }
            return;
        }

        Agent victim = damage.IsMount ? info.Agent?.MountAgent : info.Agent;
        Blow blow = damage.Blow;
        AttackCollisionData collisionData = damage.CollisionData;
        Mission mission = Mission.Current;
        if (mission == null || victim == null || !victim.IsActive() || victim.Health <= 0)
        {
            if (authorityWasVerified)
            {
                Logger.Warning(
                    "[BattleDamage] Dropping routed blow at apply: victimId={VictimId} attackerId={AttackerId} " +
                    "localController={LocalController} damage={Damage} riderKeyedMount={RiderKeyedMount} " +
                    "missionPresent={MissionPresent} victimPresent={VictimPresent} " +
                    "victimActive={VictimActive} victimHealth={VictimHealth:0.0} reason=invalid-native-victim",
                    damage.VictimAgentId,
                    damage.AttackerAgentId,
                    session.OwnControllerId,
                    damage.Blow.InflictedDamage,
                    damage.IsMount,
                    mission != null,
                    victim != null,
                    victim?.IsActive() ?? false,
                    victim?.Health ?? -1f);
            }
            return;
        }

        bool hasNativeMountedPair = !blow.BlowFlag.HasAnyFlag(BlowFlags.CanDismount)
            || agentNativeMountState.HasMountedPair(victim);
        if (RemoveIncompatibleDismountFlag(ref blow, hasNativeMountedPair))
        {
            Logger.Debug(
                "[BattleDamage] Removed stale routed dismount reaction: victimId={VictimId} " +
                "victimIndex={VictimIndex} attackerId={AttackerId}",
                damage.VictimAgentId,
                victim.Index,
                damage.AttackerAgentId);
        }

        Agent attacker = null;
        string attackerControllerId = null;
        if (damage.AttackerAgentId != Guid.Empty &&
            registry.TryGetAgentInfo(damage.AttackerAgentId, out var attackerInfo) &&
            attackerInfo.Agent != null)
        {
            attacker = attackerInfo.Agent;
            attackerControllerId = attackerInfo.CurrentAuthority;
            blow.OwnerId = attacker.Index;
        }
        else
        {
            blow.OwnerId = -1;
        }

#if DEBUG
        RecordMountedContactTelemetryForActors(attacker, victim);
#endif

        int routedDamage = blow.InflictedDamage;
        // The source calculated this blow against a puppet, so vanilla could not apply its main-agent multiplier.
        ApplyPlayerReceivedDamageMultiplier(victim, ref blow, ref collisionData);
        int inputDamage = blow.InflictedDamage;

        bool wasMissile = IsMissileDamage(damage);
        // The victim owner relays blood so a fatal effect stays ordered before its death broadcast.
        coopMissionComponent.CombatHitPresentationHandler.PresentRoutedMeleeBlood(
            victim,
            attacker,
            in blow,
            in collisionData,
            attackerControllerId);
        if (wasMissile)
        {
            blow.WeaponRecord._isMissile = false;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = -1;
        }

        float healthBefore = victim.Health;
        var mortalityBefore = victim.CurrentMortalityState;
        bool disableDying = mission.DisableDying;
        MissionMode missionMode = mission.Mode;
        BattleSpawnGate.RunWithRoutedAttackerWeapon(damage.AttackerWeapon,
            () => victim.RegisterBlow(blow, in collisionData));

        float healthAfter = victim.Health;
        float appliedDamage = healthBefore - healthAfter;
        bool activeAfter = victim.IsActive();
        var mortalityAfter = victim.CurrentMortalityState;
#if DEBUG
        RecordRoutedDamageApplied(
            damage,
            attacker,
            victim,
            routedDamage,
            inputDamage,
            appliedDamage,
            healthBefore,
            healthAfter);
#endif
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug(
                "[BattleDamage] Applied routed blow: victimId={VictimId} victimAuthority={VictimAuthority} " +
                "victimIndex={VictimIndex} victimName={VictimName} attackerId={AttackerId} " +
                "attackerAuthority={AttackerAuthority} routedDamage={RoutedDamage} inputDamage={InputDamage} " +
                "appliedDamage={AppliedDamage:0.0} " +
                "victimIsMount={VictimIsMount} riderKeyedMount={RiderKeyedMount} missile={Missile} " +
                "healthBefore={HealthBefore:0.0} healthAfter={HealthAfter:0.0} activeAfter={ActiveAfter} " +
                "mortalityBefore={MortalityBefore} mortalityAfter={MortalityAfter} " +
                "disableDying={DisableDying} missionMode={MissionMode}",
                damage.VictimAgentId,
                info.CurrentAuthority,
                victim.Index,
                victim.Name,
                damage.AttackerAgentId,
                attackerControllerId,
                routedDamage,
                inputDamage,
                appliedDamage,
                victim.IsMount,
                damage.IsMount,
                wasMissile,
                healthBefore,
                healthAfter,
                activeAfter,
                mortalityBefore,
                mortalityAfter,
                disableDying,
                missionMode);
        }

        if (inputDamage > 0 && activeAfter && appliedDamage <= 0f &&
            ShouldLogNoHealthReductionWarning(damage.VictimAgentId, out int suppressedHits))
        {
            Logger.Warning(
                "[BattleDamage] Routed blow did not reduce health: victimId={VictimId} " +
                "victimAuthority={VictimAuthority} victimIndex={VictimIndex} attackerId={AttackerId} " +
                "inputDamage={InputDamage} appliedDamage={AppliedDamage:0.0} victimIsMount={VictimIsMount} " +
                "healthBefore={HealthBefore:0.0} healthAfter={HealthAfter:0.0} " +
                "mortalityBefore={MortalityBefore} mortalityAfter={MortalityAfter} " +
                "disableDying={DisableDying} missionMode={MissionMode} suppressedHits={SuppressedHits}",
                damage.VictimAgentId,
                info.CurrentAuthority,
                victim.Index,
                damage.AttackerAgentId,
                inputDamage,
                appliedDamage,
                victim.IsMount,
                healthBefore,
                healthAfter,
                mortalityBefore,
                mortalityAfter,
                disableDying,
                missionMode,
                suppressedHits);
        }

        if (healthAfter > 0 && victim.Character is CharacterObject character && character.IsHero
            && character.HeroObject is Hero hero)
        {
            hero.HitPoints = Math.Max(1, (int)healthAfter);
        }
    }

    internal static bool RemoveIncompatibleDismountFlag(
        ref Blow blow,
        bool hasNativeMountedPair)
    {
        if (hasNativeMountedPair
            || !blow.BlowFlag.HasAnyFlag(BlowFlags.CanDismount))
        {
            return false;
        }

        blow.BlowFlag &= ~BlowFlags.CanDismount;
        return true;
    }

    private bool ShouldLogNoHealthReductionWarning(Guid victimId, out int suppressedHits)
    {
        long now = Stopwatch.GetTimestamp();
        if (!noHealthReductionWarnings.TryGetValue(victimId, out var warningState))
        {
            noHealthReductionWarnings[victimId] = new NoHealthReductionWarningState
            {
                LastWarningTimestamp = now
            };
            suppressedHits = 0;
            return true;
        }

        if (ElapsedSeconds(warningState.LastWarningTimestamp) < NoHealthReductionWarningIntervalSeconds)
        {
            warningState.SuppressedHits++;
            suppressedHits = warningState.SuppressedHits;
            return false;
        }

        suppressedHits = warningState.SuppressedHits;
        warningState.LastWarningTimestamp = now;
        warningState.SuppressedHits = 0;
        return true;
    }

    private static void ApplyPlayerReceivedDamageMultiplier(
        Agent victim,
        ref Blow blow,
        ref AttackCollisionData collisionData)
    {
        if (!victim.IsMainAgent)
            return;

        int scaledDamage = TaleWorlds.Library.MathF.Round(
            blow.InflictedDamage * Mission.Current.DamageToPlayerMultiplier);
        blow.InflictedDamage = scaledDamage;
        collisionData.InflictedDamage = scaledDamage;
    }
}
