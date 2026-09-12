#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using RopeState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    private sealed class RopeRecord
    {
        internal Attachment Attachment;
        internal long Generation;
        internal Guid OperationId;
        internal NetworkNavalLabRopeState Last;
        internal float[] CurveTarget;
        internal float CurveAngle;
        internal readonly List<int> History = new();
    }

    private readonly Dictionary<ShipAttachmentMachine, RopeRecord> ropes = new();
    private ShipAttachmentMachine[][] ropeSources;
    private ShipAttachmentPointMachine[][] ropeTargets;
    private readonly long[] ropeForceWrites = new long[3];
    private readonly long[] ropeForeignWritesFiltered = new long[3];
    private long ropeJointTicks;
    private long ropeBridgeChecksBlocked;
    private bool applyingRope;
    private Guid ropeCommandOperation;

    internal bool HasRopeExperiment => manifest.Mode == NavalLabMode.TwoClientNative;
    internal bool RopeReady => HasRopeExperiment && CanUseNativeControls && !factoryTerminal
        && factoryReleased && Mission != null && Mission == Mission.Current;

    internal bool IsFixtureRopeMachine(ShipAttachmentMachine machine) => HasRopeExperiment && machine != null
        && Array.IndexOf(Ships, machine.OwnerShip) >= 0 && machine.OwnerShip.ShipsLogic?.Mission == Mission;
    internal bool IsFixtureRope(Attachment attachment) => IsFixtureRopeMachine(attachment?.AttachmentSource);
    private bool IsOwnedRope(Attachment attachment) => IsFixtureRope(attachment)
        && attachment.AttachmentSource.OwnerShip == LocalShip;

    private void EnsureRopeInventory()
    {
        if (ropeSources != null) return;
        if (!RopeReady) throw new InvalidOperationException("rope.not_ready");
        var sources = Ships.Select(ship => ship.AttachmentMachines.OrderBy(machine => EntityKey(machine.GameEntity, ship), StringComparer.Ordinal).ToArray()).ToArray();
        var targets = Ships.Select(ship => ship.AttachmentPointMachines.OrderBy(machine => EntityKey(machine.GameEntity, ship), StringComparer.Ordinal).ToArray()).ToArray();
        if (sources.Any(row => row.Length == 0 || row.Length > 32)
            || targets.Any(row => row.Length == 0 || row.Length > NetworkNavalLabRopeState.MaxTargetStations))
            throw new InvalidOperationException("rope.unsupported_inventory:sources=" + string.Join(",", sources.Select(row => row.Length))
                + ";targets=" + string.Join(",", targets.Select(row => row.Length)));
        ropeSources = sources;
        ropeTargets = targets;
    }

    internal bool AllowRopeConnection(ShipAttachmentMachine source, ShipAttachmentPointMachine target, bool forceBridge)
    {
        if (!IsFixtureRopeMachine(source)) return true;
        if (!RopeReady || forceBridge || source.CurrentAttachment != null || source.LinkedAttachmentPointMachine?.CurrentAttachment != null) return false;
        if (target != null && (target.OwnerShip == source.OwnerShip || Array.IndexOf(Ships, target.OwnerShip) < 0
            || target.CurrentAttachment != null || target.LinkedAttachmentMachine?.CurrentAttachment != null)) return false;
        return applyingRope || source.OwnerShip == LocalShip;
    }

    internal bool AllowRopeUse(Agent agent, UsableMissionObject point) => RopeReady && agent == LocalCaptain
        && LocalShip.AttachmentMachines.Any(machine => machine.PilotStandingPoint == point);

    internal void ObserveRopeCreated(ShipAttachmentMachine machine)
    {
        if (!IsFixtureRopeMachine(machine) || applyingRope || machine.OwnerShip != LocalShip || machine.CurrentAttachment == null) return;
        EnsureRopeInventory();
        if (!ropes.TryGetValue(machine, out var record)) ropes[machine] = record = new RopeRecord();
        if (ReferenceEquals(record.Attachment, machine.CurrentAttachment)) return;
        record.Attachment = machine.CurrentAttachment;
        record.Generation++;
        record.OperationId = ropeCommandOperation;
        record.History.Clear();
        record.History.Add((int)machine.CurrentAttachment.State);
        record.Last = CaptureRope(machine, Array.IndexOf(ropeSources[OwnSlot], machine), record);
    }

    internal bool AllowRopeState(Attachment attachment, RopeState state)
    {
        if (!IsFixtureRope(attachment)) return true;
        if (state == RopeState.BridgeThrown || state == RopeState.BridgeConnected)
        {
            Reject("rope.bridge_transition_attempt");
            return false;
        }
        if (!IsOwnedRope(attachment) && !applyingRope) return false;
        if (ropes.TryGetValue(attachment.AttachmentSource, out var record) && record.Attachment == attachment
            && (record.History.Count == 0 || record.History[record.History.Count - 1] != (int)state) && record.History.Count < 8)
            record.History.Add((int)state);
        return true;
    }

    internal bool AllowRopeBridge(Attachment attachment)
    {
        if (!IsFixtureRope(attachment)) return true;
        Interlocked.Increment(ref ropeBridgeChecksBlocked);
        return false;
    }

    internal string RequestRope(NetworkNavalLabAction action)
    {
        if (!RopeReady || action.Ship != OwnSlot || action.Row || action.Rudder < 0 || action.Rudder >= 32
            || action.Rudder != Math.Truncate(action.Rudder)) return "rejected:rope_owner_or_station";
        EnsureRopeInventory();
        int index = (int)action.Rudder;
        if (index >= ropeSources[OwnSlot].Length) return "rejected:rope_source_missing";
        var source = ropeSources[OwnSlot][index];
        if (action.Kind == "rope-cut")
        {
            if (source.CurrentAttachment == null) return "already_clear";
            if (source.CurrentAttachment.AttachmentTarget == null)
                source.CurrentAttachment.SetAttachmentState(RopeState.BrokenAndWaitingForRemoval);
            else source.DisconnectAttachment();
            return "dispatched:native_rope_disconnect";
        }
        if (action.Kind != "rope-throw" && action.Kind != "rope-miss") return "rejected:rope_kind";
        if (source.PilotAgent != null || source.CurrentAttachment != null || source.LinkedAttachmentPointMachine?.CurrentAttachment != null)
            return "rejected:rope_station_busy";
        ShipAttachmentPointMachine target = null;
        if (action.Kind == "rope-throw")
        {
            if (action.RopeTargetStation < 0 || action.RopeTargetStation >= ropeTargets[1 - OwnSlot].Length)
                return "rejected:rope_target_missing";
            target = ropeTargets[1 - OwnSlot][action.RopeTargetStation];
            if (target.CurrentAttachment != null || target.LinkedAttachmentMachine?.CurrentAttachment != null)
                return "rejected:rope_target_busy";
            if (ShipAttachmentMachine.ComputePotentialAttachmentValue(source, target, checkInteractionDistance: false,
                checkConnectionBlock: false, allowWiderAngleBetweenConnections: true) <= 0)
                return "rejected:rope_alignment";
        }
        ropeCommandOperation = action.OperationId;
        try
        {
            // Explicit native target selection bypasses player aim/range selection, not flight or break behavior.
            source.ConnectWithAttachmentPointMachine(target, forceBridge: false);
            if (source.CurrentAttachment == null) return "rejected:native_rope_not_created";
            if (target == null)
            {
                var position = source.RopeVisual.GameEntity.GlobalPosition;
                var direction = source.GameEntity.GetGlobalFrame().rotation.f.NormalizedCopy();
                source.CurrentAttachment.InitializeRopeFlightDataAccordingToTargetDirection(in position, in direction);
            }
            ObserveRopeCreated(source);
            return "dispatched:native_rope_flight_not_player_throw";
        }
        finally { ropeCommandOperation = Guid.Empty; }
    }

    private NetworkNavalLabRopeState[] CaptureRopes()
    {
        if (!HasRopeExperiment) return null;
        EnsureRopeInventory();
        var result = new List<NetworkNavalLabRopeState>();
        for (int index = 0; index < ropeSources[OwnSlot].Length; index++)
        {
            var source = ropeSources[OwnSlot][index];
            ObserveRopeCreated(source);
            if (!ropes.TryGetValue(source, out var record)) continue;
            var attachment = source.CurrentAttachment;
            if (attachment == null)
            {
                if (record.Last == null) continue;
                int removed = (int)RopeState.BrokenAndWaitingForRemoval;
                if (record.History.LastOrDefault() != removed && record.History.Count < 8) record.History.Add(removed);
                var last = record.Last;
                record.Last = new NetworkNavalLabRopeState
                {
                    SourceStation = index, SourceKey = last.SourceKey, TargetStation = last.TargetStation, TargetKey = last.TargetKey,
                    Generation = record.Generation, State = removed, Length = last.Length, HookFrame = last.HookFrame,
                    History = record.History.ToArray(), OperationId = record.OperationId
                };
                result.Add(record.Last);
                continue;
            }
            record.Last = CaptureRope(source, index, record);
            result.Add(record.Last);
        }
        return result.ToArray();
    }

    private NetworkNavalLabRopeState CaptureRope(ShipAttachmentMachine source, int index, RopeRecord record)
    {
        var attachment = source.CurrentAttachment;
        if (record.History.LastOrDefault() != (int)attachment.State && record.History.Count < 8) record.History.Add((int)attachment.State);
        int targetIndex = attachment.AttachmentTarget == null ? -1 : Array.IndexOf(ropeTargets[1 - OwnSlot], attachment.AttachmentTarget);
        if (attachment.AttachmentTarget != null && targetIndex < 0) throw new InvalidOperationException("rope.target_outside_fixture");
        var hook = NetworkNavalLabOarPresentation.FromFrame(attachment.HookGlobalFrame);
        if (!NetworkNavalLabOarPresentation.ValidFrame(hook)) hook = NetworkNavalLabOarPresentation.FromFrame(source.Hook.GetGlobalFrame());
        var state = new NetworkNavalLabRopeState
        {
            SourceStation = index, SourceKey = EntityKey(source.GameEntity, LocalShip), TargetStation = targetIndex,
            TargetKey = targetIndex < 0 ? null : EntityKey(attachment.AttachmentTarget.GameEntity, Ships[1 - OwnSlot]),
            Generation = record.Generation, State = (int)attachment.State, Length = Math.Max(0, attachment._currentRopeLength),
            HookFrame = hook, History = record.History.ToArray(), OperationId = record.OperationId,
            CurveTarget = record.CurveTarget, CurveAngle = record.CurveAngle
        };
        if (!state.IsValid) throw new InvalidOperationException("rope.invalid_native_state");
        return state;
    }

    private bool ValidateRopes(int sourceSlot, NetworkNavalLabRopeState[] values)
    {
        if (!HasRopeExperiment) return values == null;
        EnsureRopeInventory();
        if (sourceSlot == OwnSlot) return false;
        // Protobuf omits an empty repeated field; no connections is a valid hull sample.
        return values == null || values.All(value => value.IsValid && value.SourceStation < ropeSources[sourceSlot].Length
            && value.SourceKey == EntityKey(ropeSources[sourceSlot][value.SourceStation].GameEntity, Ships[sourceSlot])
            && (value.TargetStation == -1 || (value.TargetStation < ropeTargets[1 - sourceSlot].Length
                && value.TargetKey == EntityKey(ropeTargets[1 - sourceSlot][value.TargetStation].GameEntity, Ships[1 - sourceSlot]))));
    }

    private void AcceptRopes(int sourceSlot, NetworkNavalLabRopeState[] values)
    {
        if (!HasRopeExperiment || values == null) return;
        applyingRope = true;
        try
        {
            foreach (var value in values)
            {
                var source = ropeSources[sourceSlot][value.SourceStation];
                if (!ropes.TryGetValue(source, out var record)) ropes[source] = record = new RopeRecord();
                if (value.Generation < record.Generation) continue;
                if (value.Generation > record.Generation)
                {
                    if (source.CurrentAttachment != null) throw new InvalidOperationException("rope.replaced_before_removal");
                    record.Generation = value.Generation;
                    record.Attachment = null;
                }
                record.Last = value;
                if (value.State == (int)RopeState.BrokenAndWaitingForRemoval)
                {
                    source.CurrentAttachment?.SetAttachmentState(RopeState.BrokenAndWaitingForRemoval);
                    continue;
                }
                if (source.CurrentAttachment == null)
                {
                    if (record.Attachment != null) throw new InvalidOperationException("rope.replica_disappeared");
                    var target = value.TargetStation < 0 ? null : ropeTargets[1 - sourceSlot][value.TargetStation];
                    if (target?.CurrentAttachment != null || target?.LinkedAttachmentMachine?.CurrentAttachment != null)
                        throw new InvalidOperationException("rope.replica_endpoint_busy");
                    source.ConnectWithAttachmentPointMachine(target, forceBridge: false);
                    if (source.CurrentAttachment == null) throw new InvalidOperationException("rope.replica_creation_refused");
                    record.Attachment = source.CurrentAttachment;
                }
                var attachment = source.CurrentAttachment;
                if (attachment.AttachmentTarget != null && (value.TargetStation < 0
                    || attachment.AttachmentTarget != ropeTargets[1 - sourceSlot][value.TargetStation]))
                    throw new InvalidOperationException("rope.endpoint_changed_within_generation");
                if (value.TargetStation >= 0 && attachment.AttachmentTarget == null)
                {
                    var target = ropeTargets[1 - sourceSlot][value.TargetStation];
                    if (target.CurrentAttachment != null || target.LinkedAttachmentMachine?.CurrentAttachment != null)
                        throw new InvalidOperationException("rope.replica_late_target_busy");
                    attachment.AttachmentTarget = target;
                    target.AssignConnection(attachment);
                }
                if (value.State == (int)RopeState.RopesPulling && attachment.ShipAttachmentJoint == null)
                {
                    var position = source.RopeVisual.GameEntity.GlobalPosition;
                    var target = attachment.AttachmentTarget;
                    var local = target.HookAttachLocalPosition;
                    attachment.InitializeShipAttachmentJoint(position, target.GameEntity.GetGlobalFrame().TransformToParent(local));
                }
                attachment.SetAttachmentState((RopeState)value.State);
                attachment._currentRopeLength = value.Length;
                attachment._hookGlobalFrame = NetworkNavalLabOarPresentation.ToFrame(value.HookFrame);
            }
        }
        finally { applyingRope = false; }
    }

    internal bool TickRope(Attachment attachment)
    {
        if (!IsFixtureRope(attachment)) return true;
        if (!RopeReady) return false;
        if (IsOwnedRope(attachment))
        {
            if (ropes.TryGetValue(attachment.AttachmentSource, out var owned)) owned.CurveTarget = null;
            return true;
        }
        if (!ropes.TryGetValue(attachment.AttachmentSource, out var record) || record.Last == null)
            throw new InvalidOperationException("rope.replica_state_missing");
        if (attachment.State == RopeState.BrokenAndWaitingForRemoval) return false;
        var source = attachment.AttachmentSource.RopeVisual.GameEntity.GlobalPosition;
        var hook = NetworkNavalLabOarPresentation.ToFrame(record.Last.HookFrame);
        var target = hook.origin;
        if (attachment.State == RopeState.RopesPulling)
            target = attachment.AttachmentTarget.GameEntity.GetGlobalFrame().TransformToParent(attachment.AttachmentTarget.HookAttachLocalPosition);
        if (record.Last.CurveTarget != null && attachment.State != RopeState.RopesPulling)
        {
            var curveTarget = new Vec3(record.Last.CurveTarget[0], record.Last.CurveTarget[1], record.Last.CurveTarget[2]);
            attachment.UpdateRopeMeshVisualAccordingToTargetPoint(in source, in curveTarget, record.Last.CurveAngle);
        }
        else attachment.AttachmentSource.RopeVisual.UpdateRopeMeshVisualAccordingToTargetPointLinear(in source, in target);
        hook.origin = target;
        attachment._hookGlobalFrame = hook;
        return false;
    }

    internal void ObserveRopeCurve(Attachment attachment, Vec3 target, float angle)
    {
        if (!IsOwnedRope(attachment) || !ropes.TryGetValue(attachment.AttachmentSource, out var record)) return;
        record.CurveTarget = new[] { target.x, target.y, target.z };
        record.CurveAngle = angle;
    }

    internal bool BeginRopeJoint(Attachment attachment)
    {
        if (!IsFixtureRope(attachment)) return false;
        if (!RopeReady) return false;
        Interlocked.Increment(ref ropeJointTicks);
        return true;
    }

    internal bool AllowRopeForce(NavalPhysics physics, int kind)
    {
        int slot = Array.FindIndex(Ships, ship => ship?.Physics == physics);
        if (slot < 0) { Reject("rope.force_outside_fixture"); return false; }
        if (factoryTerminal || nativeTerminalHold || Blocker != null) return false;
        if (!OwnsFactoryHull(slot)) { Interlocked.Increment(ref ropeForeignWritesFiltered[kind]); return false; }
        Interlocked.Increment(ref ropeForceWrites[kind]);
        return true;
    }

    private static float? RopeNumber(float value) => float.IsNaN(value) || float.IsInfinity(value) ? null : value;
    private static object RopePosition(Vec3 value) => new { x = RopeNumber(value.x), y = RopeNumber(value.y), z = RopeNumber(value.z) };

    internal object InspectRopes()
    {
        if (!RopeReady) return new { ready = false, mode = manifest.Mode.ToString(), blocker = Blocker };
        EnsureRopeInventory();
        return new
        {
            ready = true, incarnation = manifest.IncarnationId, ownSlot = OwnSlot,
            solver = "vanilla joint on each client, own endpoint writes only, native foreign velocity reads unvalidated",
            bridgeAllowed = false, nativePlankPreallocationRetained = true, keyboardAcceptance = false,
            ropeJointTicks = Interlocked.Read(ref ropeJointTicks), bridgeChecksBlocked = Interlocked.Read(ref ropeBridgeChecksBlocked),
            forceKinds = new[] { "force", "torque", "global_force_at_local_position" },
            ownForceWrites = ropeForceWrites.Select(value => value).ToArray(), foreignForceWritesFiltered = ropeForeignWritesFiltered.Select(value => value).ToArray(),
            ships = Ships.Select((ship, slot) => new
            {
                slot, originalOwner = manifest.Controllers[slot], sourceSequence = slot == OwnSlot ? shipSentSequences[slot] : shipAcceptedSequences[slot],
                activeBody = ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation(),
                sources = ropeSources[slot].Select((source, index) => new
                {
                    index, key = EntityKey(source.GameEntity, ship), position = RopePosition(source.GameEntity.GlobalPosition),
                    direction = RopePosition(source.GameEntity.GetGlobalFrame().rotation.f), nativeId = source.Id.Id,
                    busy = source.CurrentAttachment != null || source.LinkedAttachmentPointMachine?.CurrentAttachment != null,
                    state = source.CurrentAttachment?.State.ToString(), length = source.CurrentAttachment == null ? null : RopeNumber(source.CurrentAttachment._currentRopeLength),
                    minimumLength = RopeNumber(source.RopeMinLength),
                    jointDistanceError = source.CurrentAttachment?.ShipAttachmentJoint == null ? null : RopeNumber(source.CurrentAttachment.ShipAttachmentJoint.CurrentDistanceError),
                    localJointBroken = source.CurrentAttachment?.ShipAttachmentJoint?.IsBroken,
                    bridgeConnected = source.CurrentAttachment?.IsNavmeshConnected == true,
                    published = ropes.TryGetValue(source, out var record) ? record.Last : null,
                    targets = ropeTargets[1 - slot].Select((target, targetIndex) => new
                    {
                        targetIndex, distance = RopeNumber((target.GameEntity.GlobalPosition - source.GameEntity.GlobalPosition).Length),
                        nativeSelectionScore = RopeNumber(ShipAttachmentMachine.ComputePotentialAttachmentValue(source, target,
                            checkInteractionDistance: false, checkConnectionBlock: false, allowWiderAngleBetweenConnections: true))
                    }).ToArray()
                }).ToArray(),
                targets = ropeTargets[slot].Select((target, index) => new
                {
                    index, key = EntityKey(target.GameEntity, ship), position = RopePosition(target.GameEntity.GlobalPosition),
                    busy = target.CurrentAttachment != null || target.LinkedAttachmentMachine?.CurrentAttachment != null,
                    state = target.CurrentAttachment?.State.ToString()
                }).ToArray()
            }).ToArray()
        };
    }
}
#endif
