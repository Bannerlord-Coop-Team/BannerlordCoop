using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using Missions.Agents;
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
    private readonly IGuardedHitWindow guardedHitWindow;
    private readonly Func<Agent, bool?> mountAuthorityProbe;
    private readonly object inboundDamageGate = new();
    private readonly ConcurrentQueue<NetworkApplyBattleDamage> inboundDamage = new();
    private readonly Queue<PendingLocalDamage> pendingLocalDamage = new();
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
        IGuardedHitWindow guardedHitWindow)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.guardedHitWindow = guardedHitWindow;

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
            network.SendAll(new NetworkApplyBattleDamage(
                victimInfo.AgentId,
                pending.AttackerId,
                hit.Blow,
                hit.CollisionData,
                missileShotSequence: pending.ShotSequence,
                attackerWeapon: pending.AttackerWeapon));
            return;
        }

        if (hit.IsMount &&
            hit.Victim?.RiderAgent is Agent rider &&
            registry.TryGetAgentInfo(rider, out var riderInfo))
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
            network.SendAll(new NetworkApplyBattleDamage(
                riderInfo.AgentId,
                pending.AttackerId,
                hit.Blow,
                hit.CollisionData,
                isMount: true,
                missileShotSequence: pending.ShotSequence,
                attackerWeapon: pending.AttackerWeapon));
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

        if (inputDamage > 0 && activeAfter && appliedDamage <= 0f)
        {
            Logger.Warning(
                "[BattleDamage] Routed blow did not reduce health: victimId={VictimId} " +
                "victimAuthority={VictimAuthority} victimIndex={VictimIndex} attackerId={AttackerId} " +
                "inputDamage={InputDamage} appliedDamage={AppliedDamage:0.0} victimIsMount={VictimIsMount} " +
                "healthBefore={HealthBefore:0.0} healthAfter={HealthAfter:0.0} " +
                "mortalityBefore={MortalityBefore} mortalityAfter={MortalityAfter} " +
                "disableDying={DisableDying} missionMode={MissionMode}",
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
                missionMode);
        }

        if (healthAfter > 0 && victim.Character is CharacterObject character && character.IsHero
            && character.HeroObject is Hero hero)
        {
            hero.HitPoints = Math.Max(1, (int)healthAfter);
        }
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
