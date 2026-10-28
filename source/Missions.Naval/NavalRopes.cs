using Common.Logging;
using Missions.Messages;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using RopeState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;

namespace Missions.Naval;

/// <summary>
/// [Game thread] Rope replication: the source hull's owner publishes each throw station's rope, and every other client's
/// replica of it changes only as those states apply. Static because the rope patches read its records.
/// </summary>
internal static class NavalRopes
{
    internal enum ReplicaStep
    {
        Ignore,
        Remove,
        Recreate,
        Update,
    }

    /// <summary>What this client knows of one throw station's rope: the owner's attachment and generation, or the last applied state.</summary>
    internal sealed class RopeRecord
    {
        internal Attachment Attachment;
        internal long Generation;
        internal BattleRopeState Last;
        internal float[] CurveTarget;
        internal float CurveAngle;
        internal int DecorationPlanks;
        internal int RemovedRepeats;
        internal DateTime? LastIncludedSampleUtc;
        internal DateTime? SamplesOmittedUtc;
        internal DateTime? LastAcceptedSampleUtc;
        internal DateTime? ReplicaRemovalAppliedUtc;
        internal DateTime? ReplicaRemovalPreviousSampleUtc;
        internal string ReplicaRemovalReason;
    }

    /// <summary>
    /// How many samples repeat a station's removal: 5 s at the 20 Hz sample rate. A peer that misses them all still removes
    /// its replica, from the first accepted sample that omits the station.
    /// </summary>
    internal const int RemovedRepeatSamples = 100;

    private static readonly Dictionary<ShipAttachmentMachine, RopeRecord> records = new Dictionary<ShipAttachmentMachine, RopeRecord>();
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(NavalRopes));

    private static long replicaRejects, replicaFaults, finalsApplied;
    private static string lastReplicaReject;

    /// <summary>Set while a replica rope may change: an owner state applies, or a local break of a rope whose hull left runs.</summary>
    internal static bool BypassReplicaGates { get; private set; }

    /// <summary>Forgets every rope record and counter when the battle ends.</summary>
    internal static void Clear()
    {
        records.Clear();
        RopeForcePatch.ClearCounters();
        NavalRopeRemoval.Clear();
        replicaRejects = replicaFaults = finalsApplied = 0;
        lastReplicaReject = null;
        BypassReplicaGates = false;
    }

    /// <summary>Runs a rope change that the replica gates must let through.</summary>
    internal static void PastReplicaGates(Action change)
    {
        BypassReplicaGates = true;
        try
        {
            change();
        }
        finally
        {
            BypassReplicaGates = false;
        }
    }

    /// <summary>Forgets the records of a hull's throw stations.</summary>
    internal static void Forget(MissionShip hull)
    {
        foreach (var machine in hull.AttachmentMachines)
            records.Remove(machine);
    }

    /// <summary>The station's record, or null while it has none.</summary>
    internal static RopeRecord RecordOf(ShipAttachmentMachine machine) =>
        machine != null && records.TryGetValue(machine, out var record) ? record : null;

    /// <summary>Whether the station sits on another owner's copy here.</summary>
    internal static bool IsReplicaMachine(ShipAttachmentMachine machine) => machine != null && NavalForeignHulls.Contains(machine.OwnerShip);

    /// <summary>Whether the rope is a replica of another owner's rope.</summary>
    internal static bool IsReplica(Attachment attachment) => IsReplicaMachine(attachment?.AttachmentSource);

    /// <summary>Only the owner of the source hull decides a rope; a replica changes only while an owner state applies.</summary>
    internal static bool AllowsChange(bool sourceIsCopy, bool applyingOwnerState) => !sourceIsCopy || applyingOwnerState;

    /// <summary>What an owner state does to a replica holding <paramref name="replicaGeneration"/>.</summary>
    internal static ReplicaStep Decide(long replicaGeneration, bool replicaExists, BattleRopeState value)
    {
        if (value.Generation < replicaGeneration) return ReplicaStep.Ignore;
        if (value.State == BattleRopeState.Removed) return replicaExists ? ReplicaStep.Remove : ReplicaStep.Ignore;
        if (value.Generation > replicaGeneration && replicaExists) return ReplicaStep.Recreate;
        return ReplicaStep.Update;
    }

    // ---- Owner side ----

    /// <summary>Opens a new generation when an owned station holds a rope its record hasn't seen.</summary>
    internal static void ObserveCreated(ShipAttachmentMachine machine)
    {
        if (!CoopNavalMissionScope.IsActive || BypassReplicaGates || machine?.CurrentAttachment == null || IsReplicaMachine(machine)) return;

        if (!records.TryGetValue(machine, out var record)) records[machine] = record = new RopeRecord();
        if (ReferenceEquals(record.Attachment, machine.CurrentAttachment)) return;

        record.Attachment = machine.CurrentAttachment;
        record.Generation++;
        record.CurveTarget = null;
        record.LastIncludedSampleUtc = record.SamplesOmittedUtc = null;
    }

    /// <summary>The owned hull's rope at every station that ever threw, for its next sample; null when there is none.</summary>
    internal static BattleRopeState[] Capture(MissionShip hull, Func<MissionObject, Guid> shipIdOf)
    {
        var result = new List<BattleRopeState>();
        foreach (var source in hull.AttachmentMachines)
        {
            ObserveCreated(source);
            if (!records.TryGetValue(source, out var record) || record.Generation == 0) continue;

            var attachment = source.CurrentAttachment;
            if (attachment == null || !ReferenceEquals(attachment, record.Attachment))
            {
                // The station's rope is gone: repeat its removal for a while, so every replica converges.
                if (record.Last == null || !RepeatRemoval(record)) continue;
            }
            else
            {
                record.Last = CaptureAttachment(hull, source, attachment, record, shipIdOf);
                record.RemovedRepeats = 0;
            }

            if (record.Last != null)
            {
                result.Add(record.Last);
                record.LastIncludedSampleUtc = DateTime.UtcNow;
            }
            if (result.Count == BattleRopeState.MaxRopesPerHull) break;
        }

        return result.Count == 0 ? null : result.ToArray();
    }

    // Turns the station's last state into its removal; false once the removal has gone out for the whole repeat window.
    private static bool RepeatRemoval(RopeRecord record)
    {
        if (record.Last.State != BattleRopeState.Removed) record.Last = Removed(record.Last);
        if (record.RemovedRepeats == RemovedRepeatSamples)
        {
            if (record.SamplesOmittedUtc == null) record.SamplesOmittedUtc = DateTime.UtcNow;
            return false;
        }

        record.RemovedRepeats++;
        return true;
    }

    /// <summary>The removal of a station's last published rope, at that rope's generation.</summary>
    internal static BattleRopeState Removed(BattleRopeState last) => new BattleRopeState
    {
        SourceKey = last.SourceKey,
        Generation = last.Generation,
        State = BattleRopeState.Removed,
        TargetShipId = last.TargetShipId,
        TargetKey = last.TargetKey,
        Length = last.Length,
        HookFrame = last.HookFrame,
    };

    // The owner's live rope as peers rebuild it, or the last good state while this frame's would be rejected.
    private static BattleRopeState CaptureAttachment(MissionShip hull, ShipAttachmentMachine source, Attachment attachment,
        RopeRecord record, Func<MissionObject, Guid> shipIdOf)
    {
        var target = attachment.AttachmentTarget;
        Guid targetShipId = target == null ? Guid.Empty : shipIdOf(target.OwnerShip);
        string targetKey = targetShipId == Guid.Empty ? null : NavalShipEngine.EntityPath(target.GameEntity, target.OwnerShip.GameEntity);
        var hook = NetworkBattleShipSample.FromFrame(attachment.HookGlobalFrame);
        if (!NetworkBattleShipSample.IsValidFrame(hook)) hook = NetworkBattleShipSample.FromFrame(source.Hook.GetGlobalFrame());
        string sourceKey = NavalShipEngine.EntityPath(source.GameEntity, hull.GameEntity);

        // Peers cannot rebuild a rope to a hull without a network identity, so for them it is gone.
        if (target != null && targetKey == null)
            return new BattleRopeState { SourceKey = sourceKey, Generation = record.Generation, State = BattleRopeState.Removed, HookFrame = hook };

        var state = new BattleRopeState
        {
            SourceKey = sourceKey,
            Generation = record.Generation,
            State = (int)attachment.State,
            TargetShipId = targetKey == null ? Guid.Empty : targetShipId,
            TargetKey = targetKey,
            Length = Math.Max(0f, attachment._currentRopeLength),
            HookFrame = hook,
            CurveTarget = record.CurveTarget,
            CurveAngle = record.CurveAngle,
            PlankFlight = CapturePlankFlight(attachment),
            DecorationPlanks = record.DecorationPlanks,
        };

        // A state peers would reject (a transient frame) keeps the last good one.
        return state.IsValid ? state : record.Last;
    }

    // The plank's flight clock while it is thrown or connected, else null.
    private static float[] CapturePlankFlight(Attachment attachment)
    {
        if (attachment.State != RopeState.BridgeThrown && attachment.State != RopeState.BridgeConnected) return null;

        var value = attachment._bridgeFlightData;
        return new[]
        {
            value.DtSinceFlightStart, value.CurveLerpVelocity, value.CurveLerpValue, value.ThrowFinishValue,
            value.CurrentFrameTotalLightTime, value.CurrentFrameInitialVelocity.x, value.CurrentFrameInitialVelocity.y,
            value.CurrentFrameInitialVelocity.z,
        };
    }

    // ---- Replica side ----

    /// <summary>Applies an owner's rope set to its copy here; a final set also lays the replicas out once.</summary>
    internal static void Apply(MissionShip copy, BattleRopeState[] values, Func<Guid, MissionObject> hullOf, bool final)
    {
        if (final && values == null) return;

        var sampleUtc = DateTime.UtcNow;
        BypassReplicaGates = true;
        try
        {
            foreach (var value in values ?? Array.Empty<BattleRopeState>())
                ApplyCounted(value.SourceKey, () => ApplyOne(copy, value, hullOf, sampleUtc));
            if (!final)
            {
                RemoveOmitted(copy, values, sampleUtc);
                foreach (var source in copy.AttachmentMachines)
                {
                    if (records.TryGetValue(source, out var record)) record.LastAcceptedSampleUtc = sampleUtc;
                }
            }
        }
        finally
        {
            BypassReplicaGates = false;
        }

        if (!final) return;

        PresentFinal(copy);
        finalsApplied++;
    }

    // An owner's sample carries every live rope, so a replica it omits lost its rope while this client missed the removal.
    private static void RemoveOmitted(MissionShip copy, BattleRopeState[] values, DateTime sampleUtc)
    {
        foreach (var source in copy.AttachmentMachines)
        {
            var attachment = source.CurrentAttachment;
            if (attachment == null || attachment.State == RopeState.BrokenAndWaitingForRemoval) continue;
            if (!records.TryGetValue(source, out var record) || record.Last == null || record.Last.State == BattleRopeState.Removed) continue;

            string key = record.Last.SourceKey;
            if (values != null && values.Any(value => value.SourceKey == key)) continue;

            record.Last = Removed(record.Last);
            ApplyCounted(key, () =>
            {
                attachment.SetAttachmentState(RopeState.BrokenAndWaitingForRemoval);
                RecordReplicaRemoval(record, "omitted", sampleUtc);
                return null;
            });
        }
    }

    // Keeps the removal apply and preceding accepted sample clocks for the stalled-peer check.
    private static void RecordReplicaRemoval(RopeRecord record, string reason, DateTime sampleUtc)
    {
        if (record.ReplicaRemovalAppliedUtc != null) return;

        record.ReplicaRemovalAppliedUtc = sampleUtc;
        record.ReplicaRemovalPreviousSampleUtc = record.LastAcceptedSampleUtc;
        record.ReplicaRemovalReason = reason;
    }

    // Runs one rope's apply, so one bad rope doesn't stop the others; counts a reject, and logs the mission's first fault.
    private static void ApplyCounted(string sourceKey, Func<string> apply)
    {
        string reject;
        try
        {
            reject = apply();
        }
        catch (Exception exception)
        {
            lastReplicaReject = sourceKey + ":fault:" + exception.GetType().Name;
            if (++replicaFaults == 1)
                Logger.Warning(exception, "[NavalSync] Applying the owner's rope at {Source} on a copy threw; later faults are only counted",
                    sourceKey);
            return;
        }

        if (reject == null) return;

        replicaRejects++;
        lastReplicaReject = sourceKey + ":" + reject;
    }

    // Applies one owner state to its station's replica; returns why it was rejected, or null.
    private static string ApplyOne(MissionShip copy, BattleRopeState value, Func<Guid, MissionObject> hullOf, DateTime sampleUtc)
    {
        var source = Resolve<ShipAttachmentMachine>(copy, value.SourceKey);
        if (source == null) return "source_station_missing";

        if (!records.TryGetValue(source, out var record)) records[source] = record = new RopeRecord();
        var step = Decide(record.Generation, source.CurrentAttachment != null, value);
        if (step == ReplicaStep.Ignore) return null;

        record.Last = value;
        record.DecorationPlanks = value.DecorationPlanks;
        if (step == ReplicaStep.Remove)
        {
            // The copy's machine tick destroys it next frame, as vanilla does on the owner.
            source.CurrentAttachment.SetAttachmentState(RopeState.BrokenAndWaitingForRemoval);
            RecordReplicaRemoval(record, "removed_state", sampleUtc);
            return null;
        }

        if (step == ReplicaStep.Recreate)
        {
            // The owner replaced this station's rope between two samples.
            source.CurrentAttachment.Destroy();
            source.CheckCurrentAttachmentAndInitializeRopeBoundingBox();
            record.Attachment = null;
        }
        if (value.Generation > record.Generation)
        {
            record.ReplicaRemovalAppliedUtc = record.ReplicaRemovalPreviousSampleUtc = null;
            record.ReplicaRemovalReason = null;
        }
        record.Generation = value.Generation;

        var target = value.HasTarget && hullOf(value.TargetShipId) is MissionShip targetHull
            ? Resolve<ShipAttachmentPointMachine>(targetHull, value.TargetKey)
            : null;
        string reject = TargetReject(value, target != null) ?? Connect(source, target, record) ?? HookLanded(source.CurrentAttachment, target);
        if (reject != null) return reject;

        Present(source, target, value);
        if (value.State == (int)RopeState.BrokenAndWaitingForRemoval)
            RecordReplicaRemoval(record, "broken_state", sampleUtc);
        return null;
    }

    /// <summary>Why a state can't apply with or without its resolved target point, or null.</summary>
    internal static string TargetReject(BattleRopeState value, bool targetResolved)
    {
        if (value.HasTarget && !targetResolved) return "target_station_missing";
        if (!targetResolved && (value.State == BattleRopeState.RopesPulling || value.IsPlank)) return "target_required";
        return null;
    }

    // Creates the station's replica rope through vanilla's connect path when it has none; returns why it couldn't, or null.
    private static string Connect(ShipAttachmentMachine source, ShipAttachmentPointMachine target, RopeRecord record)
    {
        if (source.CurrentAttachment != null) return null;
        if (IsBusy(target)) return "target_busy";

        source.ConnectWithAttachmentPointMachine(target, forceBridge: false);
        if (source.CurrentAttachment == null) return "replica_creation_refused";

        record.Attachment = source.CurrentAttachment;
        return null;
    }

    // Hooks a replica created in flight to the point the owner's hook landed on; returns why it couldn't, or null.
    private static string HookLanded(Attachment attachment, ShipAttachmentPointMachine target)
    {
        if (attachment.AttachmentTarget != null && attachment.AttachmentTarget != target) return "target_changed_within_generation";
        if (target == null || attachment.AttachmentTarget != null) return null;
        if (IsBusy(target)) return "late_target_busy";

        attachment.AttachmentTarget = target;
        target.AssignConnection(attachment);
        return null;
    }

    // Sets the replica to the owner's state: its joint while pulling or planked, the plank, state, length and hook frame.
    private static void Present(ShipAttachmentMachine source, ShipAttachmentPointMachine target, BattleRopeState value)
    {
        var attachment = source.CurrentAttachment;
        if ((value.State == BattleRopeState.RopesPulling || value.IsPlank) && attachment.ShipAttachmentJoint == null)
        {
            var position = source.RopeVisual.GameEntity.GlobalPosition;
            var frame = target.GameEntity.GetGlobalFrame();
            attachment.InitializeShipAttachmentJoint(position, frame.TransformToParent(target.HookAttachLocalPosition));
        }

        ApplyPlank(attachment, value);
        attachment.SetAttachmentState((RopeState)value.State);
        attachment._currentRopeLength = value.Length;
        attachment._hookGlobalFrame = NetworkBattleShipSample.ToFrame(value.HookFrame);
    }

    /// <summary>Whether a rope already holds the point or the station linked to it.</summary>
    internal static bool IsBusy(ShipAttachmentPointMachine target) =>
        target != null && (target.CurrentAttachment != null || target.LinkedAttachmentMachine?.CurrentAttachment != null);

    // The script at a content path below the hull, or null when the path doesn't resolve.
    private static T Resolve<T>(MissionShip hull, string key) where T : TaleWorlds.Engine.ScriptComponentBehavior
    {
        var entity = NavalShipEngine.ResolvePath(hull.GameEntity, key);
        return entity.IsValid ? entity.GetFirstScriptOfType<T>() : null;
    }

    // Sets a replica plank to the owner's flight clock and runs vanilla's side effects once it connects.
    private static void ApplyPlank(Attachment attachment, BattleRopeState value)
    {
        if (!value.IsPlank) return;

        if (attachment.State != RopeState.BridgeThrown && attachment.State != RopeState.BridgeConnected)
            attachment.StartBridgeThrowAnimation();
        var f = value.PlankFlight;
        attachment._bridgeFlightData = new Attachment.BridgeFlightData
        {
            DtSinceFlightStart = f[0], CurveLerpVelocity = f[1], CurveLerpValue = f[2], ThrowFinishValue = f[3],
            CurrentFrameTotalLightTime = f[4], CurrentFrameInitialVelocity = new Vec3(f[5], f[6], f[7]),
        };
        if (value.State != BattleRopeState.BridgeConnected || attachment.State == RopeState.BridgeConnected) return;

        // Vanilla's connection side effects (bridge navmesh, connected ship islands, unseating nearby oars) run as on
        // the owner; the apply happens outside the machine tick that would otherwise announce the connection.
        attachment.ConnectBridge();
        attachment.AttachmentSource.OwnerShip.OnShipConnected(attachment);
        attachment.AttachmentTarget.OwnerShip.OnShipConnected(attachment);
    }

    // Lays a replica out once at its owner's last state, for an owner that sends no more samples.
    private static void PresentFinal(MissionShip copy)
    {
        foreach (var source in copy.AttachmentMachines)
        {
            var attachment = source.CurrentAttachment;
            if (attachment == null || !records.TryGetValue(source, out var record) || record.Last == null) continue;

            if (attachment.State == RopeState.BridgeThrown)
            {
                attachment.ArrangePlanksMT();
                attachment.ArrangePlanks();
            }
            else if (attachment.State != RopeState.BridgeConnected && attachment.State != RopeState.BrokenAndWaitingForRemoval)
            {
                UpdateReplicaRopeVisual(attachment, record);
            }
        }
    }

    // ---- Patch callbacks ----

    /// <summary>Keeps the owner's throw curve for the next sample.</summary>
    internal static void ObserveCurve(Attachment attachment, Vec3 target, float angle)
    {
        if (IsReplica(attachment) || attachment?.AttachmentSource == null
            || !records.TryGetValue(attachment.AttachmentSource, out var record))
            return;

        record.CurveTarget = new[] { target.x, target.y, target.z };
        record.CurveAngle = angle;
    }

    // A replica connects only as its owner's state applies, and no rope hooks a hull that left.
    internal static bool AllowConnection(ShipAttachmentMachine source, ShipAttachmentPointMachine target) =>
        AllowsChange(IsReplicaMachine(source), BypassReplicaGates)
        && (target == null || !NavalRopeRemoval.IsMissingOrRemoved(target.OwnerShip));

    // A replica's state changes only as its owner's state applies.
    internal static bool AllowState(Attachment attachment) => AllowsChange(IsReplica(attachment), BypassReplicaGates);

    // A replica disconnects only as its owner's state applies.
    internal static bool AllowDisconnect(ShipAttachmentMachine source) => AllowsChange(IsReplicaMachine(source), BypassReplicaGates);

    // Bridge eligibility, endpoint retargeting and breaking are the owner's decisions.
    internal static bool AllowOwnerDecision(Attachment attachment) => !IsReplica(attachment);

    // Retargeting a copy's ropes is its owner's decision.
    internal static bool AllowConnectionRetarget(MissionShip hull) => !NavalForeignHulls.Contains(hull);

    // The owner's plank flight clock arrives with each sample; a replica never runs its own completion clock.
    internal static bool TickPlankFlight(Attachment attachment)
    {
        if (!IsReplica(attachment)) return true;

        attachment.ArrangePlanksMT();
        attachment.ArrangePlanks();
        return false;
    }

    // Replica ropes are drawn from the owner's state; planks keep vanilla's layout tick. A rope to a hull that left breaks.
    internal static bool TickRope(Attachment attachment)
    {
        var source = attachment.AttachmentSource;
        if (NavalRopeRemoval.BreakIfEndGone(attachment)) return false;

        if (!IsReplica(attachment))
        {
            // The throw curve observer refills this when vanilla draws a flying rope this tick.
            if (source != null && records.TryGetValue(source, out var owned)) owned.CurveTarget = null;
            return true;
        }

        if (!records.TryGetValue(source, out var record) || record.Last == null) return false;
        if (attachment.State == RopeState.BrokenAndWaitingForRemoval) return false;
        if (attachment.State == RopeState.BridgeThrown || attachment.State == RopeState.BridgeConnected) return true;

        UpdateReplicaRopeVisual(attachment, record);
        return false;
    }

    // Draws a replica rope from the owner's last state: its hook frame, and its throw curve while it flies.
    private static void UpdateReplicaRopeVisual(Attachment attachment, RopeRecord record)
    {
        var source = attachment.AttachmentSource.RopeVisual.GameEntity.GlobalPosition;
        var hook = NetworkBattleShipSample.ToFrame(record.Last.HookFrame);
        var target = hook.origin;
        if (attachment.State == RopeState.RopesPulling && attachment.AttachmentTarget != null)
            target = attachment.AttachmentTarget.GameEntity.GetGlobalFrame().TransformToParent(attachment.AttachmentTarget.HookAttachLocalPosition);

        var curve = record.Last.CurveTarget;
        if (curve != null && attachment.State != RopeState.RopesPulling)
        {
            var curveTarget = new Vec3(curve[0], curve[1], curve[2]);
            attachment.UpdateRopeMeshVisualAccordingToTargetPoint(in source, in curveTarget, record.Last.CurveAngle);
        }
        else
        {
            attachment.AttachmentSource.RopeVisual.UpdateRopeMeshVisualAccordingToTargetPointLinear(in source, in target);
        }

        hook.origin = target;
        attachment._hookGlobalFrame = hook;
    }

    // ---- Diagnostics ----

    /// <summary>Every hull's stations and the rope counters, for rope_status.</summary>
    internal static object Inspect(IEnumerable<MissionShip> hulls, Func<MissionObject, Guid> shipIdOf) => new
    {
        forceKinds = new[] { "force", "torque", "global_force_at_local_position" },
        ownForceWrites = RopeForcePatch.OwnForceWrites(),
        foreignFiltered = RopeForcePatch.ForeignForcesFiltered(),
        replicaRejects,
        replicaFaults,
        lastReplicaReject,
        jointStepsSkipped = NavalRopeRemoval.JointStepsSkipped,
        finalsApplied,
        hulls = hulls.Select(hull => new
        {
            shipId = shipIdOf(hull),
            role = NavalForeignHulls.Contains(hull) ? "copy" : "owner",
            sources = hull.AttachmentMachines.Select(source => InspectSource(hull, source, shipIdOf))
                .Where(source => source != null).ToArray(),
        }).ToArray(),
    };

    // One station's rope as rope_status shows it, or null for a station that never threw and has no user.
    private static object InspectSource(MissionShip hull, ShipAttachmentMachine source, Func<MissionObject, Guid> shipIdOf)
    {
        records.TryGetValue(source, out var record);
        var attachment = source.CurrentAttachment;
        if (attachment == null && record == null && source.PilotAgent == null) return null;

        var target = attachment?.AttachmentTarget;
        return new
        {
            key = NavalShipEngine.EntityPath(source.GameEntity, hull.GameEntity),
            user = source.PilotAgent?.Name,
            generation = record?.Generation ?? 0,
            state = attachment?.State.ToString(),
            target = target == null ? null
                : shipIdOf(target.OwnerShip) + "/" + NavalShipEngine.EntityPath(target.GameEntity, target.OwnerShip.GameEntity),
            published = record?.Last == null ? null
                : ((RopeState)record.Last.State).ToString() + "@" + record.Last.Generation.ToString(CultureInfo.InvariantCulture),
            bridgeConnected = attachment?.State == RopeState.BridgeConnected,
            navmeshConnected = attachment?.IsNavmeshConnected == true,
            plankNavmesh = attachment?._navMeshBridge?.WeakEntity.IsValid == true,
            plankOccupants = attachment?._navMeshBridge == null ? (int?)null : PlankOccupants(attachment),
            decorationPlanks = record?.DecorationPlanks ?? 0,
            lastIncludedSampleUtc = record?.LastIncludedSampleUtc,
            samplesOmittedUtc = record?.SamplesOmittedUtc,
            removedRepeatSamples = record?.RemovedRepeats ?? 0,
            lastAcceptedSampleUtc = record?.LastAcceptedSampleUtc,
            replicaRemovalAppliedUtc = record?.ReplicaRemovalAppliedUtc,
            replicaRemovalPreviousSampleUtc = record?.ReplicaRemovalPreviousSampleUtc,
            replicaRemovalReason = record?.ReplicaRemovalReason,
        };
    }

    // The live agents standing on the rope's plank.
    private static int PlankOccupants(Attachment attachment) =>
        Mission.Current?.Agents.Count(agent => agent.IsActive() && NavalShipEngine.IsOnPlank(agent, attachment)) ?? 0;
}
