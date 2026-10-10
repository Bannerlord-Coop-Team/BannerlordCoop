using Missions.Messages;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using RopeState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;

namespace Missions.Naval;

/// <summary>
/// [Game thread] Owner-authored ropes and planks. The owner of a rope's source hull runs
/// vanilla's throw, pull and plank logic and publishes each throw station with its hull samples; every other client
/// keeps a replica on its copy of that hull whose state changes and rope ticks are frozen and replaced by the owner's
/// state. Joint forces reach only hulls this client simulates. Static because the rope patches read it.
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

    private sealed class RopeRecord
    {
        internal Attachment Attachment;
        internal long Generation;
        internal BattleRopeState Last;
        internal float[] CurveTarget;
        internal float CurveAngle;
        internal int DecorationPlanks;
    }

    private static readonly Dictionary<ShipAttachmentMachine, RopeRecord> records = new Dictionary<ShipAttachmentMachine, RopeRecord>();
    private static readonly long[] ownForceWrites = new long[3];
    private static readonly long[] foreignForceFiltered = new long[3];
    private static long replicaRejects, finalsApplied;
    private static string lastReplicaReject;

    /// <summary>Set while a received owner state is applied, so the replica gates let it through.</summary>
    internal static bool Applying { get; private set; }

    internal static void Clear()
    {
        records.Clear();
        Array.Clear(ownForceWrites, 0, ownForceWrites.Length);
        Array.Clear(foreignForceFiltered, 0, foreignForceFiltered.Length);
        replicaRejects = finalsApplied = 0;
        lastReplicaReject = null;
        Applying = false;
    }

    internal static bool IsReplicaMachine(ShipAttachmentMachine machine) => machine != null && NavalForeignHulls.Contains(machine.OwnerShip);

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

    internal static void ObserveCreated(ShipAttachmentMachine machine)
    {
        if (!CoopNavalMissionScope.IsActive || Applying || machine?.CurrentAttachment == null || IsReplicaMachine(machine)) return;

        if (!records.TryGetValue(machine, out var record)) records[machine] = record = new RopeRecord();
        if (ReferenceEquals(record.Attachment, machine.CurrentAttachment)) return;

        record.Attachment = machine.CurrentAttachment;
        record.Generation++;
        record.CurveTarget = null;
    }

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
                // The station's rope is gone: repeat its removal until the next throw, so every replica converges.
                if (record.Last == null) continue;
                if (record.Last.State != BattleRopeState.Removed) record.Last = Removed(record.Last);
            }
            else
            {
                record.Last = CaptureAttachment(hull, source, attachment, record, shipIdOf);
            }

            if (record.Last != null) result.Add(record.Last);
            if (result.Count == BattleRopeState.MaxRopesPerHull) break;
        }

        return result.Count == 0 ? null : result.ToArray();
    }

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

    internal static void Apply(MissionShip copy, BattleRopeState[] values, Func<Guid, MissionObject> hullOf, bool final)
    {
        if (values == null) return;

        Applying = true;
        try
        {
            foreach (var value in values)
            {
                try
                {
                    ApplyOne(copy, value, hullOf);
                }
                catch (Exception exception)
                {
                    replicaRejects++;
                    lastReplicaReject = value.SourceKey + ":" + exception.Message;
                }
            }
        }
        finally
        {
            Applying = false;
        }

        if (!final) return;

        PresentFinal(copy);
        finalsApplied++;
    }

    private static void ApplyOne(MissionShip copy, BattleRopeState value, Func<Guid, MissionObject> hullOf)
    {
        var source = Resolve<ShipAttachmentMachine>(copy, value.SourceKey);
        if (source == null) throw new InvalidOperationException("source_station_missing");

        if (!records.TryGetValue(source, out var record)) records[source] = record = new RopeRecord();
        var step = Decide(record.Generation, source.CurrentAttachment != null, value);
        if (step == ReplicaStep.Ignore) return;

        record.Last = value;
        record.DecorationPlanks = value.DecorationPlanks;
        if (step == ReplicaStep.Remove)
        {
            // The copy's machine tick destroys it next frame, as vanilla does on the owner.
            source.CurrentAttachment.SetAttachmentState(RopeState.BrokenAndWaitingForRemoval);
            return;
        }

        if (step == ReplicaStep.Recreate)
        {
            // The owner replaced this station's rope between two samples.
            source.CurrentAttachment.Destroy();
            source.CheckCurrentAttachmentAndInitializeRopeBoundingBox();
            record.Attachment = null;
        }
        record.Generation = value.Generation;

        var target = value.HasTarget && hullOf(value.TargetShipId) is MissionShip targetHull
            ? Resolve<ShipAttachmentPointMachine>(targetHull, value.TargetKey)
            : null;
        if (value.HasTarget && target == null) throw new InvalidOperationException("target_station_missing");
        if (target == null && (value.State == BattleRopeState.RopesPulling || value.IsPlank))
            throw new InvalidOperationException("target_required");

        if (source.CurrentAttachment == null)
        {
            if (IsBusy(target)) throw new InvalidOperationException("target_busy");
            source.ConnectWithAttachmentPointMachine(target, forceBridge: false);
            if (source.CurrentAttachment == null) throw new InvalidOperationException("replica_creation_refused");
            record.Attachment = source.CurrentAttachment;
        }

        var attachment = source.CurrentAttachment;
        if (attachment.AttachmentTarget != null && attachment.AttachmentTarget != target)
            throw new InvalidOperationException("target_changed_within_generation");
        if (target != null && attachment.AttachmentTarget == null)
        {
            // The owner's hook landed after the replica was created in flight.
            if (IsBusy(target)) throw new InvalidOperationException("late_target_busy");
            attachment.AttachmentTarget = target;
            target.AssignConnection(attachment);
        }

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

    private static bool IsBusy(ShipAttachmentPointMachine target) =>
        target != null && (target.CurrentAttachment != null || target.LinkedAttachmentMachine?.CurrentAttachment != null);

    private static T Resolve<T>(MissionShip hull, string key) where T : TaleWorlds.Engine.ScriptComponentBehavior
    {
        var entity = NavalShipEngine.ResolvePath(hull.GameEntity, key);
        return entity.IsValid ? entity.GetFirstScriptOfType<T>() : null;
    }

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

    internal static void ObserveCurve(Attachment attachment, Vec3 target, float angle)
    {
        if (IsReplica(attachment) || attachment?.AttachmentSource == null
            || !records.TryGetValue(attachment.AttachmentSource, out var record))
            return;

        record.CurveTarget = new[] { target.x, target.y, target.z };
        record.CurveAngle = angle;
    }

    internal static bool AllowConnection(ShipAttachmentMachine source) => AllowsChange(IsReplicaMachine(source), Applying);

    internal static bool AllowState(Attachment attachment) => AllowsChange(IsReplica(attachment), Applying);

    internal static bool AllowDisconnect(ShipAttachmentMachine source) => AllowsChange(IsReplicaMachine(source), Applying);

    // Bridge eligibility, endpoint retargeting and breaking are the owner's decisions.
    internal static bool AllowOwnerDecision(Attachment attachment) => !IsReplica(attachment);

    internal static bool AllowConnectionRetarget(MissionShip hull) => !NavalForeignHulls.Contains(hull);

    // The owner's plank flight clock arrives with each sample; a replica never runs its own completion clock.
    internal static bool TickPlankFlight(Attachment attachment)
    {
        if (!IsReplica(attachment)) return true;

        attachment.ArrangePlanksMT();
        attachment.ArrangePlanks();
        return false;
    }

    // Replica ropes are drawn from the owner's state; planks keep vanilla's layout tick.
    internal static bool TickRope(Attachment attachment)
    {
        var source = attachment.AttachmentSource;
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

    /// <summary>A rope joint writes force only to a hull this client simulates; a copy follows its owner's frames.</summary>
    internal static bool AllowsForce(bool bodyIsCopy) => !bodyIsCopy;

    internal static bool AllowForce(NavalPhysics physics, int kind)
    {
        if (!CoopNavalMissionScope.IsActive) return true;

        bool copy = NavalForeignHulls.All.Any(ship => ship.Physics == physics);
        Interlocked.Increment(ref (copy ? foreignForceFiltered : ownForceWrites)[kind]);
        return AllowsForce(copy);
    }

    // Plank decoration draws from a seed every client derives alike, so a replica plank looks like the owner's.
    internal static Random PlankCosmeticRandom(Attachment attachment, string purpose)
    {
        var source = attachment?.AttachmentSource;
        if (!CoopNavalMissionScope.IsActive || source?.OwnerShip == null) return null;

        records.TryGetValue(source, out var record);
        if (purpose == "AddRopesToBridge" && !IsReplica(attachment) && record != null)
            record.DecorationPlanks = attachment._numberOfPlanksNeeded;

        long generation = record?.Generation ?? 0;
        if (!Applying && !ReferenceEquals(record?.Attachment, attachment)) generation++;
        string key = NavalShipEngine.EntityPath(source.GameEntity, source.OwnerShip.GameEntity) + ":"
            + generation.ToString(CultureInfo.InvariantCulture) + ":" + purpose;
        int seed = 17;
        unchecked
        {
            foreach (char value in key) seed = (seed * 31) + value;
        }

        return new Random(seed);
    }

    internal static int DecorationCount(Attachment attachment) =>
        IsReplica(attachment) && records.TryGetValue(attachment.AttachmentSource, out var record) && record.DecorationPlanks > 0
            ? record.DecorationPlanks
            : attachment._numberOfPlanksNeeded;

    // ---- Diagnostics ----

    internal static object Inspect(IEnumerable<MissionShip> hulls, Func<MissionObject, Guid> shipIdOf) => new
    {
        forceKinds = new[] { "force", "torque", "global_force_at_local_position" },
        ownForceWrites = ownForceWrites.ToArray(),
        foreignFiltered = foreignForceFiltered.ToArray(),
        replicaRejects,
        lastReplicaReject,
        finalsApplied,
        hulls = hulls.Select(hull => new
        {
            shipId = shipIdOf(hull),
            role = NavalForeignHulls.Contains(hull) ? "copy" : "owner",
            sources = hull.AttachmentMachines.Select(source => InspectSource(hull, source, shipIdOf))
                .Where(source => source != null).ToArray(),
        }).ToArray(),
    };

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
        };
    }

    private static int PlankOccupants(Attachment attachment) =>
        Mission.Current?.Agents.Count(agent => agent.IsActive() && NavalShipEngine.IsOnPlank(agent, attachment)) ?? 0;
}
