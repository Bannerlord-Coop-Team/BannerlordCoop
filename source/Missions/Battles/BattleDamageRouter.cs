using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using Missions.Missiles.Message;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Combat damage routing for a coop battle: a local troop hitting a puppet is suppressed locally
/// (<c>BattleBlowInterceptPatch</c>) and routed to the puppet's owner, which applies it authoritatively — so
/// each agent's life/death is decided on exactly one client and the battles don't diverge. Mounts are
/// registered agents too, so a hit on another owner's horse routes by the HORSE's own id — pinned to the
/// exact horse that was struck, immune to its rider dismounting or swapping horses in flight.
/// </summary>
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
    private readonly Dictionary<(Guid AgentId, long ShotSequence), ReconstructionInfo> reconstructionsBySequence = new();
    private readonly Dictionary<(Guid AgentId, int MissileIndex), ReconstructionInfo> reconstructionsBySource = new();
    private readonly Queue<ReconstructionInfo> reconstructionHistory = new();
    private readonly Queue<DeferredDamage> deferredDamage = new();
    private readonly Dictionary<Guid, int> deferredDamageByVictim = new();
    private readonly object inboundDamageGate = new();
    private readonly Queue<NetworkApplyBattleDamage> inboundDamage = new();
    private System.Threading.Timer deferredFlushTimer;
    private long deferredTimerGeneration;
    private long presentationEpoch;
    private float presentationTimeSeconds;
    private bool inboundDamageScheduled;
    private bool disposed;
    private bool closing;

    private const int MaxDeferredDamage = 4096;
    private const int MaxReconstructionHistory = 8192;
    private const int DeferredDamageTimeoutMs = 4000;
    private const int MinimumPresentationEpochs = 2;
    private const float UnknownShotGraceSeconds = 0.5f;
    private const float MinimumFlightSeconds = 0.05f;
    private const float MaximumFlightSeconds = 2.5f;
    private const float MaximumPresentationTickSeconds = 0.1f;
    private const float LegacyCorrelationSeconds = 0.5f;
    private const float ReconstructionRetentionSeconds = 30f;

    private readonly struct ReconstructionInfo
    {
        public Guid AgentId { get; }
        public int SourceMissileIndex { get; }
        public long ShotSequence { get; }
        public Vec3 Position { get; }
        public Vec3 Direction { get; }
        public float BaseSpeed { get; }
        public float Speed { get; }
        public bool IsFastForwarded { get; }
        public float RemainingFlightSeconds { get; }
        public long PresentationEpoch { get; }
        public float PresentationTime { get; }

        public ReconstructionInfo(MissileReconstructed reconstructed, long presentationEpoch, float presentationTime)
        {
            AgentId = reconstructed.AgentId;
            SourceMissileIndex = reconstructed.SourceMissileIndex;
            ShotSequence = reconstructed.ShotSequence;
            Position = reconstructed.Position;
            Direction = reconstructed.Direction;
            BaseSpeed = reconstructed.BaseSpeed;
            Speed = reconstructed.Speed;
            IsFastForwarded = reconstructed.IsFastForwarded;
            RemainingFlightSeconds = reconstructed.RemainingFlightSeconds;
            PresentationEpoch = presentationEpoch;
            PresentationTime = presentationTime;
        }
    }

    private readonly struct DeferredDamage
    {
        public NetworkApplyBattleDamage Damage { get; }
        public long EnqueuedTimestamp { get; }
        public long EarliestPresentationEpoch { get; }
        public float FallbackPresentationDeadline { get; }
        public bool RequiresPresentation { get; }

        public DeferredDamage(NetworkApplyBattleDamage damage, long earliestPresentationEpoch,
            float fallbackPresentationDeadline)
        {
            Damage = damage;
            EnqueuedTimestamp = Stopwatch.GetTimestamp();
            EarliestPresentationEpoch = earliestPresentationEpoch;
            FallbackPresentationDeadline = fallbackPresentationDeadline;
            RequiresPresentation = IsMissileDamage(damage);
        }
    }

    public BattleDamageRouter(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        ICoopMissionComponent coopMissionComponent,
        IBattleSession session)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;

        messageBroker.Subscribe<BattlePuppetHit>(Handle_BattlePuppetHit);
        messageBroker.Subscribe<NetworkApplyBattleDamage>(Handle_NetworkApplyBattleDamage);
        messageBroker.Subscribe<MissileReconstructed>(Handle_MissileReconstructed);

        // Let the (static, DI-less) intercept patch gate mount hits by the horse's OWN registration — a
        // registered horse under a remote authority is suppressed+routed even when masterless. Kept as a
        // field so Dispose only clears a probe this instance installed.
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
            inboundDamage.Clear();
            inboundDamageScheduled = false;
        }

        messageBroker.Unsubscribe<BattlePuppetHit>(Handle_BattlePuppetHit);
        messageBroker.Unsubscribe<NetworkApplyBattleDamage>(Handle_NetworkApplyBattleDamage);
        messageBroker.Unsubscribe<MissileReconstructed>(Handle_MissileReconstructed);

        CancelDeferredFlush();
        reconstructionsBySequence.Clear();
        reconstructionsBySource.Clear();
        reconstructionHistory.Clear();
        deferredDamage.Clear();
        deferredDamageByVictim.Clear();

        if (BattleSpawnGate.MountAuthorityProbe == mountAuthorityProbe)
            BattleSpawnGate.MountAuthorityProbe = null;
    }

    private void Handle_MissileReconstructed(MessagePayload<MissileReconstructed> payload)
    {
        if (disposed || closing)
            return;

        ReconstructionInfo reconstruction = new ReconstructionInfo(payload.What, presentationEpoch, presentationTimeSeconds);
        if (reconstruction.AgentId != Guid.Empty)
        {
            if (reconstruction.ShotSequence != 0)
                reconstructionsBySequence[(reconstruction.AgentId, reconstruction.ShotSequence)] = reconstruction;
            else
                // Only legacy sequence-zero shots enter the index lookup. A failed lookup from a new sender
                // must not accidentally correlate against some other sequenced shot that reused the same slot.
                reconstructionsBySource[(reconstruction.AgentId, reconstruction.SourceMissileIndex)] = reconstruction;
        }

        reconstructionHistory.Enqueue(reconstruction);
        while (reconstructionHistory.Count > MaxReconstructionHistory)
            RemoveOldestReconstruction();
    }

    /// <summary>
    /// Advances the presentation clock and applies hits once their locally replayed projectile has had time to
    /// traverse the corresponding shot. The per-tick clamp prevents a long game-thread stall from consuming the
    /// whole visual flight before even one recovered frame can be displayed.
    /// </summary>
    public void Tick(float dt)
    {
        if (disposed || closing)
            return;

        presentationEpoch++;
        if (!float.IsNaN(dt) && !float.IsInfinity(dt) && dt > 0f)
            presentationTimeSeconds += Math.Min(dt, MaximumPresentationTickSeconds);

        int deferredCount = deferredDamage.Count;
        var blockedVictims = new HashSet<Guid>();
        for (int i = 0; i < deferredCount; i++)
        {
            DeferredDamage deferred = deferredDamage.Dequeue();
            NetworkApplyBattleDamage damage = deferred.Damage;
            if (blockedVictims.Contains(damage.VictimAgentId) || IsWaitingForMissilePresentation(deferred))
            {
                deferredDamage.Enqueue(deferred);
                blockedVictims.Add(damage.VictimAgentId);
            }
            else
            {
                ApplyDeferredDamage(damage);
            }
        }

        if (deferredDamage.Count == 0)
            CancelDeferredFlush();

        while (reconstructionHistory.Count > 0
            && reconstructionHistory.Peek().PresentationTime + ReconstructionRetentionSeconds <= presentationTimeSeconds)
            RemoveOldestReconstruction();
    }

    private void RemoveOldestReconstruction()
    {
        ReconstructionInfo expired = reconstructionHistory.Dequeue();

        if (expired.AgentId == Guid.Empty)
            return;

        if (expired.ShotSequence != 0
            && reconstructionsBySequence.TryGetValue((expired.AgentId, expired.ShotSequence), out ReconstructionInfo currentSequence)
            && currentSequence.PresentationEpoch == expired.PresentationEpoch)
        {
            reconstructionsBySequence.Remove((expired.AgentId, expired.ShotSequence));
        }

        if (reconstructionsBySource.TryGetValue((expired.AgentId, expired.SourceMissileIndex), out ReconstructionInfo currentSource)
            && currentSource.ShotSequence == expired.ShotSequence
            && currentSource.PresentationEpoch == expired.PresentationEpoch)
        {
            reconstructionsBySource.Remove((expired.AgentId, expired.SourceMissileIndex));
        }
    }

    public void FlushForMissionEnd()
    {
        List<NetworkApplyBattleDamage> receivedDamage;
        lock (inboundDamageGate)
        {
            if (disposed || closing)
                return;

            closing = true;
            receivedDamage = new List<NetworkApplyBattleDamage>(inboundDamage);
            inboundDamage.Clear();
            inboundDamageScheduled = false;
        }

        // Deferred entries were received first, so apply them before packets that had reached this router but
        // whose game-thread callback had not run yet. This closes the mission-end window without letting a
        // late callback silently disappear after the result is committed.
        FlushAllDeferredDamage();
        foreach (NetworkApplyBattleDamage damage in receivedDamage)
        {
            if (!IsLocallyAuthoritativeFor(damage.VictimAgentId))
                continue;

            try
            {
                ApplyNetworkDamage(damage);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply routed battle damage during mission-end flush");
            }
        }
    }

    // Whether a mount agent is registered and remotely owned: true → puppet-gated (suppress + route), false →
    // ours (apply locally), null → unregistered (the patch falls back to rider-keyed gating).
    private bool? ProbeMountAuthority(Agent mount)
    {
        if (!coopMissionComponent.AgentRegistry.TryGetAgentInfo(mount, out var info)) return null;
        return info.CurrentAuthority != session.OwnControllerId;
    }

    // [Attacker's node] A local troop hit a puppet (suppressed locally by BattleBlowInterceptPatch). Route the
    // WHOLE blow to the victim's owner; only the owner re-applies it. The attacker's network id rides along so
    // the owner can re-map the (per-client) attacker index to its local agent. The victim is the agent actually
    // struck — for a registered mount that is the HORSE itself, routed by its own id so the blow stays pinned
    // to it even if its rider dismounts or swaps horses before the owner applies it. Only an UNregistered horse
    // is keyed off its rider's id (IsMount), leaving the owner to resolve its current MountAgent.
    private void Handle_BattlePuppetHit(MessagePayload<BattlePuppetHit> payload)
    {
        var registry = coopMissionComponent.AgentRegistry;

        GameThread.RunSafe(() =>
        {
            if (disposed || closing)
                return;

            Guid attackerId = Guid.Empty;
            if (payload.What.Attacker != null && registry.TryGetAgentInfo(payload.What.Attacker, out var attackerInfo))
                attackerId = attackerInfo.AgentId;

            long missileShotSequence = 0;
            if (payload.What.Blow.IsMissile)
            {
                int sourceMissileIndex = payload.What.Blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex;
                if (coopMissionComponent.MissileHandler.TryGetLocalShot(sourceMissileIndex,
                    out Guid shotAgentId, out missileShotSequence))
                {
                    // The shooter can leave or be deregistered while its projectile is still in flight. The
                    // launch record preserves the original network identity even when FindAgentWithIndex did not.
                    attackerId = shotAgentId;
                }
                else
                {
                    Logger.Warning("Could not correlate local missile hit at source index {MissileIndex}; sending sequence-zero fallback",
                        sourceMissileIndex);
                }
            }

            if (registry.TryGetAgentInfo(payload.What.Victim, out var victimInfo))
            {
                Logger.Information("[DeathDiag] Routing puppet hit to owner {Owner}: victim={Victim}, dmg={Dmg}, mount={IsMount}, shot={ShotSequence}",
                    victimInfo.CurrentAuthority, victimInfo.AgentId, payload.What.Blow.InflictedDamage,
                    payload.What.IsMount, missileShotSequence);
                network.SendAll(new NetworkApplyBattleDamage(victimInfo.AgentId, attackerId, payload.What.Blow,
                    payload.What.CollisionData, missileShotSequence: missileShotSequence));
                return;
            }

            // An unregistered horse under a registered rider: legacy rider-keyed route — the owner resolves
            // its rider's CURRENT MountAgent at apply time.
            if (payload.What.IsMount
                && payload.What.Victim?.RiderAgent is Agent rider
                && registry.TryGetAgentInfo(rider, out var riderInfo))
            {
                Logger.Information("[DeathDiag] Routing unregistered-mount hit via rider {Rider} to owner {Owner}: dmg={Dmg}", riderInfo.AgentId, riderInfo.CurrentAuthority, payload.What.Blow.InflictedDamage);
                network.SendAll(new NetworkApplyBattleDamage(riderInfo.AgentId, attackerId, payload.What.Blow,
                    payload.What.CollisionData, isMount: true, missileShotSequence: missileShotSequence));
                return;
            }

            Logger.Information("[DeathDiag] Local hit on a puppet that is not in our registry — cannot route it");
        });
    }

    // [Owner] Another client's troop hit one of OUR agents — a troop, or a registered horse addressed by its
    // own id. (IsMount is the fallback for an UNregistered horse, addressed via its rider's id; the rider's
    // current MountAgent is resolved here at apply time.) Re-apply the real blow through Agent.RegisterBlow so
    // the engine resolves damage, hit reaction, ragdoll and lethal death. AgentDeathReporter then sends
    // the normal death/casualty sync. Non-owners ignore it. No synthetic blow.
    private void Handle_NetworkApplyBattleDamage(MessagePayload<NetworkApplyBattleDamage> payload)
    {
        NetworkApplyBattleDamage damage = payload.What;
        if (IsMissileDamage(damage)
            && damage.AttackerAgentId != Guid.Empty
            && damage.MissileShotSequence != 0)
        {
            Vec3 impactPosition = damage.Blow.GlobalPosition;
            if (!IsFinite(impactPosition))
                impactPosition = damage.CollisionData.CollisionGlobalPosition;

            Vec3 impactVelocity = damage.Blow.WeaponRecord.Velocity;
            if (!IsFinite(impactVelocity) || impactVelocity.LengthSquared <= 0.0001f)
                impactVelocity = damage.CollisionData.MissileVelocity;

            // Record this before dispatching to the game thread. If the matching shot is still waiting there,
            // reconstruction can show only its terminal segment instead of replaying an already-finished flight.
            coopMissionComponent.MissileHandler.RecordImpactHint(damage.AttackerAgentId,
                damage.MissileShotSequence, damage.VictimAgentId, damage.IsMount, impactPosition, impactVelocity);
        }

        bool scheduleDrain;
        lock (inboundDamageGate)
        {
            if (disposed || closing)
                return;

            inboundDamage.Enqueue(damage);
            scheduleDrain = !inboundDamageScheduled;
            inboundDamageScheduled = true;
        }

        if (scheduleDrain)
            GameThread.RunSafe(DrainInboundDamage, context: nameof(DrainInboundDamage));
    }

    private void DrainInboundDamage()
    {
        while (true)
        {
            NetworkApplyBattleDamage damage;
            lock (inboundDamageGate)
            {
                if (disposed || closing)
                {
                    inboundDamage.Clear();
                    inboundDamageScheduled = false;
                    return;
                }

                if (inboundDamage.Count == 0)
                {
                    inboundDamageScheduled = false;
                    return;
                }

                damage = inboundDamage.Dequeue();
            }

            try
            {
                if (!IsLocallyAuthoritativeFor(damage.VictimAgentId))
                    continue;

                if (ShouldDeferDamage(damage))
                {
                    DeferDamage(damage);
                    Logger.Debug("Deferring routed damage for victim {VictimId} behind missile presentation", damage.VictimAgentId);
                    continue;
                }

                ApplyNetworkDamage(damage);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to process routed battle damage");
            }
        }
    }

    private bool ShouldDeferDamage(NetworkApplyBattleDamage damage)
    {
        if (deferredDamageByVictim.ContainsKey(damage.VictimAgentId))
            return true;

        if (!IsMissileDamage(damage))
            return false;

        if (IsReconstructionPending(damage))
            return true;

        if (!TryGetReconstruction(damage, out ReconstructionInfo reconstruction))
            return true;

        return IsReconstructionStillPresenting(damage, reconstruction);
    }

    private bool IsReconstructionPending(NetworkApplyBattleDamage damage)
    {
        if (!IsMissileDamage(damage) || damage.AttackerAgentId == Guid.Empty)
            return false;

        int sourceMissileIndex = GetSourceMissileIndex(damage);
        return coopMissionComponent.MissileHandler.IsReconstructionPending(
            damage.AttackerAgentId, damage.MissileShotSequence, sourceMissileIndex);
    }

    private bool TryGetReconstruction(NetworkApplyBattleDamage damage, out ReconstructionInfo reconstruction)
    {
        reconstruction = default;
        if (!IsMissileDamage(damage) || damage.AttackerAgentId == Guid.Empty)
            return false;

        if (damage.MissileShotSequence != 0)
            return reconstructionsBySequence.TryGetValue(
                (damage.AttackerAgentId, damage.MissileShotSequence), out reconstruction);

        int sourceMissileIndex = GetSourceMissileIndex(damage);
        return reconstructionsBySource.TryGetValue(
                (damage.AttackerAgentId, sourceMissileIndex), out reconstruction)
            && presentationTimeSeconds - reconstruction.PresentationTime <= LegacyCorrelationSeconds;
    }

    private bool IsLocallyAuthoritativeFor(Guid victimAgentId)
    {
        return coopMissionComponent.AgentRegistry.TryGetAgentInfo(victimAgentId, out var info)
            && info.CurrentAuthority == session.OwnControllerId;
    }

    private void DeferDamage(NetworkApplyBattleDamage damage)
    {
        if (deferredDamage.Count >= MaxDeferredDamage)
        {
            Logger.Warning("Missile presentation queue reached {Capacity}; applying its oldest hit to make room", MaxDeferredDamage);
            ApplyDeferredDamage(deferredDamage.Dequeue().Damage);
        }

        bool hasReconstruction = TryGetReconstruction(damage, out _);
        long earliestPresentationEpoch = presentationEpoch + (IsMissileDamage(damage) ? MinimumPresentationEpochs : 0);
        float fallbackDeadline = presentationTimeSeconds
            + (IsMissileDamage(damage) && !hasReconstruction ? UnknownShotGraceSeconds : 0f);
        deferredDamage.Enqueue(new DeferredDamage(damage, earliestPresentationEpoch, fallbackDeadline));
        deferredDamageByVictim.TryGetValue(damage.VictimAgentId, out int count);
        deferredDamageByVictim[damage.VictimAgentId] = count + 1;
        EnsureDeferredFlushScheduled();
    }

    private void ApplyDeferredDamage(NetworkApplyBattleDamage damage)
    {
        try
        {
            ApplyNetworkDamage(damage);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply deferred routed battle damage");
        }
        finally
        {
            ReleaseDeferredVictim(damage.VictimAgentId);
        }
    }

    private void FlushAllDeferredDamage()
    {
        while (deferredDamage.Count > 0)
            ApplyDeferredDamage(deferredDamage.Dequeue().Damage);

        CancelDeferredFlush();
    }

    private void EnsureDeferredFlushScheduled()
    {
        if (deferredFlushTimer != null || deferredDamage.Count == 0 || disposed || closing)
            return;

        long oldestTimestamp = long.MaxValue;
        foreach (DeferredDamage deferred in deferredDamage)
            oldestTimestamp = Math.Min(oldestTimestamp, deferred.EnqueuedTimestamp);

        double elapsedMs = ElapsedMilliseconds(oldestTimestamp);
        int dueMs = Math.Max(1, (int)Math.Ceiling(DeferredDamageTimeoutMs - elapsedMs));
        long generation = ++deferredTimerGeneration;
        deferredFlushTimer = new System.Threading.Timer(_ =>
        {
            GameThread.RunSafe(() => FlushExpiredDeferredDamage(generation),
                context: nameof(FlushExpiredDeferredDamage));
        }, null, dueMs, System.Threading.Timeout.Infinite);
    }

    private void FlushExpiredDeferredDamage(long generation)
    {
        if (disposed || generation != deferredTimerGeneration)
            return;

        deferredFlushTimer?.Dispose();
        deferredFlushTimer = null;

        int expiredCount = 0;
        int count = deferredDamage.Count;
        var blockedVictims = new HashSet<Guid>();
        for (int i = 0; i < count; i++)
        {
            DeferredDamage deferred = deferredDamage.Dequeue();
            Guid victimId = deferred.Damage.VictimAgentId;
            bool expired = ElapsedMilliseconds(deferred.EnqueuedTimestamp) >= DeferredDamageTimeoutMs;
            if (blockedVictims.Contains(victimId) || (deferred.RequiresPresentation && !expired))
            {
                deferredDamage.Enqueue(deferred);
                blockedVictims.Add(victimId);
                continue;
            }

            ApplyDeferredDamage(deferred.Damage);
            expiredCount++;
        }

        if (expiredCount > 0)
            Logger.Warning("Missile presentation wait exceeded {TimeoutMs}ms; flushed {Count} deferred hits",
                DeferredDamageTimeoutMs, expiredCount);

        EnsureDeferredFlushScheduled();
    }

    private static double ElapsedMilliseconds(long timestamp) =>
        (Stopwatch.GetTimestamp() - timestamp) * 1000d / Stopwatch.Frequency;

    private void CancelDeferredFlush()
    {
        deferredTimerGeneration++;
        deferredFlushTimer?.Dispose();
        deferredFlushTimer = null;
    }

    private void ReleaseDeferredVictim(Guid victimAgentId)
    {
        if (!deferredDamageByVictim.TryGetValue(victimAgentId, out int count))
            return;

        if (count <= 1)
            deferredDamageByVictim.Remove(victimAgentId);
        else
            deferredDamageByVictim[victimAgentId] = count - 1;
    }

    private bool IsWaitingForMissilePresentation(DeferredDamage deferred)
    {
        if (presentationEpoch < deferred.EarliestPresentationEpoch)
            return true;

        NetworkApplyBattleDamage damage = deferred.Damage;
        if (!IsMissileDamage(damage))
            return false;

        if (IsReconstructionPending(damage))
            return true;

        if (TryGetReconstruction(damage, out ReconstructionInfo reconstruction))
            return IsReconstructionStillPresenting(damage, reconstruction);

        return presentationTimeSeconds < deferred.FallbackPresentationDeadline;
    }

    private bool IsReconstructionStillPresenting(NetworkApplyBattleDamage damage, ReconstructionInfo reconstruction)
    {
        if (presentationEpoch < reconstruction.PresentationEpoch + MinimumPresentationEpochs)
            return true;

        float flightSeconds = EstimateFlightSeconds(damage, reconstruction);
        return presentationTimeSeconds < reconstruction.PresentationTime + flightSeconds;
    }

    private static float EstimateFlightSeconds(NetworkApplyBattleDamage damage, ReconstructionInfo reconstruction)
    {
        if (reconstruction.IsFastForwarded && IsFinite(reconstruction.RemainingFlightSeconds))
        {
            return (float)Math.Max(0d,
                Math.Min(MaximumFlightSeconds, reconstruction.RemainingFlightSeconds));
        }

        Vec3 impactPosition = damage.Blow.GlobalPosition;
        if (!IsFinite(impactPosition))
            impactPosition = damage.CollisionData.CollisionGlobalPosition;

        Vec3 displacement = impactPosition - reconstruction.Position;
        if (!IsFinite(displacement))
            displacement = damage.CollisionData.CollisionGlobalPosition - reconstruction.Position;

        double horizontalDistance = Math.Sqrt(
            (double)displacement.X * displacement.X + (double)displacement.Y * displacement.Y);
        double distance = Math.Sqrt(
            (double)displacement.X * displacement.X
            + (double)displacement.Y * displacement.Y
            + (double)displacement.Z * displacement.Z);

        double launchSpeed = IsUsableSpeed(reconstruction.Speed)
            ? reconstruction.Speed
            : IsUsableSpeed(reconstruction.BaseSpeed) ? reconstruction.BaseSpeed : 0d;
        if (!IsFinite(distance) || !IsUsableSpeed(launchSpeed))
            return MinimumFlightSeconds;

        Vec3 impactVelocity = damage.Blow.WeaponRecord.Velocity;
        if (!IsFinite(impactVelocity) || impactVelocity.LengthSquared <= 0.0001f)
            impactVelocity = damage.CollisionData.MissileVelocity;
        double impactSpeed = IsFinite(impactVelocity)
            ? Math.Sqrt((double)impactVelocity.X * impactVelocity.X
                + (double)impactVelocity.Y * impactVelocity.Y
                + (double)impactVelocity.Z * impactVelocity.Z)
            : 0d;

        double launchHorizontalSpeed = Math.Sqrt(
            (double)reconstruction.Direction.X * reconstruction.Direction.X
            + (double)reconstruction.Direction.Y * reconstruction.Direction.Y) * launchSpeed;
        double impactHorizontalSpeed = IsFinite(impactVelocity)
            ? Math.Sqrt((double)impactVelocity.X * impactVelocity.X
                + (double)impactVelocity.Y * impactVelocity.Y)
            : 0d;

        double estimatedSeconds;
        if (horizontalDistance > 0.05d
            && IsUsableSpeed(launchHorizontalSpeed)
            && IsUsableSpeed(impactHorizontalSpeed))
        {
            estimatedSeconds = 2d * horizontalDistance / (launchHorizontalSpeed + impactHorizontalSpeed);
        }
        else if (IsUsableSpeed(impactSpeed))
        {
            estimatedSeconds = 2d * distance / (launchSpeed + impactSpeed);
        }
        else
        {
            estimatedSeconds = distance / launchSpeed;
        }

        if (!IsFinite(estimatedSeconds))
            return MinimumFlightSeconds;

        return (float)Math.Max(MinimumFlightSeconds, Math.Min(MaximumFlightSeconds, estimatedSeconds));
    }

    private static bool IsFinite(Vec3 value) =>
        IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);

    private static bool IsMissileDamage(NetworkApplyBattleDamage damage) =>
        damage.IsMissile || damage.Blow.IsMissile;

    private static int GetSourceMissileIndex(NetworkApplyBattleDamage damage) =>
        damage.IsMissile
            ? damage.SourceMissileIndex
            : damage.Blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex;

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool IsUsableSpeed(double value) => IsFinite(value) && value > 1d;

    private void ApplyNetworkDamage(NetworkApplyBattleDamage damage)
    {
        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(damage.VictimAgentId, out var info)) return;
        if (info.CurrentAuthority != session.OwnControllerId) return;

        var victim = damage.IsMount ? info.Agent?.MountAgent : info.Agent;
        var blow = damage.Blow;
        var collisionData = damage.CollisionData;
        var attackerId = damage.AttackerAgentId;

        if (Mission.Current == null || victim == null || !victim.IsActive() || victim.Health <= 0) return;

        // Re-map the attacker index to OUR local agent (indices are per-client); -1 if not resolvable here.
        if (attackerId != Guid.Empty && registry.TryGetAgentInfo(attackerId, out var attackerInfo) && attackerInfo.Agent != null)
            blow.OwnerId = attackerInfo.Agent.Index;
        else
            blow.OwnerId = -1;

        // The visual missile has a receiver-local index, while the routed blow carries the shooter's source
        // index. Clear the missile lookup before RegisterBlow so Mission.OnAgentHit cannot index the wrong entry.
        bool wasMissile = IsMissileDamage(damage);
        if (wasMissile)
        {
            blow.WeaponRecord._isMissile = false;
            blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = -1;
        }

        Logger.Information("[BattleSync] Applying routed blow to {Agent}: dmg={Damage}, missile={Missile}, health={Health}",
            victim.Name, blow.InflictedDamage, wasMissile, victim.Health);
        victim.RegisterBlow(blow, in collisionData);

        // A hero's in-mission Agent.Health only propagates to the campaign Hero.HitPoints when the agent is
        // removed (Mission.OnAgentRemoved), so mirror surviving hero damage back to the campaign object.
        if (victim.Health > 0 && victim.Character is CharacterObject character && character.IsHero && character.HeroObject is Hero hero)
            hero.HitPoints = Math.Max(1, (int)victim.Health);
    }
}
