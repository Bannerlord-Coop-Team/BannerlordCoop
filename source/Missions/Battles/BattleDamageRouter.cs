using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using Missions.Missiles.Handlers;
using Missions.Missiles.Message;
using Serilog;
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
}

/// <inheritdoc cref="IBattleDamageRouter"/>
public class BattleDamageRouter : IBattleDamageRouter
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleDamageRouter>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly IBattleSession session;
    private readonly Func<Agent, bool?> mountAuthorityProbe;
    private readonly object inboundDamageGate = new();
    private readonly ConcurrentQueue<NetworkApplyBattleDamage> inboundDamage = new();
    private readonly Queue<DeferredDamage> deferredDamage = new();
    private readonly Dictionary<(Guid AgentId, long ShotSequence), ReconstructionInfo> reconstructions = new();
    private readonly Queue<(Guid AgentId, long ShotSequence)> reconstructionHistory = new();
    private long presentationEpoch;
    private float presentationTime;
    private bool disposed;
    private bool closing;

    private const int MinimumPresentationEpochs = 2;
    private const int MaxReconstructionHistory = 4096;
    private const double DamageTimeoutSeconds = 4d;
    private const float UnknownShotGraceSeconds = 0.5f;
    private const float MinimumFlightSeconds = 0.05f;
    private const float MaximumFlightSeconds = 2.5f;
    private const float MaximumTickSeconds = 0.1f;

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

    public BattleDamageRouter(IBattleNetwork network, IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent, IBattleSession session)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;

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
        reconstructions.Clear();
        reconstructionHistory.Clear();
        while (inboundDamage.TryDequeue(out _)) { }

        if (BattleSpawnGate.MountAuthorityProbe == mountAuthorityProbe)
            BattleSpawnGate.MountAuthorityProbe = null;
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

        while (deferredDamage.Count > 0)
            ApplyDeferredDamage(deferredDamage.Dequeue().Damage);
        while (inboundDamage.TryDequeue(out NetworkApplyBattleDamage damage))
            TryApplyNetworkDamage(damage);
    }

    private void Handle_BattlePuppetHit(MessagePayload<BattlePuppetHit> payload)
    {
        if (disposed || closing)
            return;

        var registry = coopMissionComponent.AgentRegistry;
        Guid attackerId = Guid.Empty;
        if (payload.What.Attacker != null
            && registry.TryGetAgentInfo(payload.What.Attacker, out var attackerInfo))
        {
            attackerId = attackerInfo.AgentId;
        }

        long shotSequence = 0;
        if (payload.What.Blow.IsMissile)
        {
            int missileIndex = payload.What.Blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex;
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

        GameThread.RunSafe(() =>
        {
            if (disposed || closing)
                return;

            if (registry.TryGetAgentInfo(payload.What.Victim, out var victimInfo))
            {
                network.SendAll(new NetworkApplyBattleDamage(victimInfo.AgentId, attackerId,
                    payload.What.Blow, payload.What.CollisionData,
                    missileShotSequence: shotSequence));
                return;
            }

            if (payload.What.IsMount && payload.What.Victim?.RiderAgent is Agent rider
                && registry.TryGetAgentInfo(rider, out var riderInfo))
            {
                network.SendAll(new NetworkApplyBattleDamage(riderInfo.AgentId, attackerId,
                    payload.What.Blow, payload.What.CollisionData, isMount: true,
                    missileShotSequence: shotSequence));
                return;
            }

            Logger.Warning("Local hit on an unregistered puppet could not be routed");
        });
    }

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
            if (!IsLocallyAuthoritativeFor(damage.VictimAgentId))
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
                TryApplyNetworkDamage(damage);
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

    private bool IsLocallyAuthoritativeFor(Guid victimId) =>
        coopMissionComponent.AgentRegistry.TryGetAgentInfo(victimId, out var info)
        && info.CurrentAuthority == session.OwnControllerId;

    private void ApplyDeferredDamage(NetworkApplyBattleDamage damage)
    {
        TryApplyNetworkDamage(damage);
        if (damage.AttackerAgentId != Guid.Empty && damage.MissileShotSequence != 0)
            reconstructions.Remove((damage.AttackerAgentId, damage.MissileShotSequence));
    }

    private void TryApplyNetworkDamage(NetworkApplyBattleDamage damage)
    {
        try
        {
            ApplyNetworkDamage(damage);
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

    private void ApplyNetworkDamage(NetworkApplyBattleDamage damage)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(damage.VictimAgentId, out var info)
            || info.CurrentAuthority != session.OwnControllerId)
        {
            return;
        }

        Agent victim = damage.IsMount ? info.Agent?.MountAgent : info.Agent;
        Blow blow = damage.Blow;
        AttackCollisionData collisionData = damage.CollisionData;
        if (Mission.Current == null || victim == null || !victim.IsActive() || victim.Health <= 0)
            return;

        if (damage.AttackerAgentId != Guid.Empty
            && registry.TryGetAgentInfo(damage.AttackerAgentId, out var attackerInfo)
            && attackerInfo.Agent != null)
        {
            blow.OwnerId = attackerInfo.Agent.Index;
        }
        else
        {
            blow.OwnerId = -1;
        }

        bool wasMissile = IsMissileDamage(damage);
        if (wasMissile)
        {
            blow.WeaponRecord._isMissile = false;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = -1;
        }

        Logger.Information("[BattleSync] Applying routed blow to {Agent}: dmg={Damage}, missile={Missile}, health={Health}",
            victim.Name, blow.InflictedDamage, wasMissile, victim.Health);
        victim.RegisterBlow(blow, in collisionData);

        if (victim.Health > 0 && victim.Character is CharacterObject character && character.IsHero
            && character.HeroObject is Hero hero)
        {
            hero.HitPoints = Math.Max(1, (int)victim.Health);
        }
    }
}
