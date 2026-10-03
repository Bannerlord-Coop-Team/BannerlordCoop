#if DEBUG
using System;
using System.Linq;
using Missions.Messages;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using RopeState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    internal bool AllowPlankCheck(Attachment attachment, ref bool forceBridge)
    {
        if (!IsFixtureRope(attachment)) return true;
        if (!RopeReady || !IsOwnedRope(attachment) || attachment.State != RopeState.RopesPulling
            || attachment.AttachmentTarget == null) return false;
        if (forceBridge) return true;
        var source = attachment.AttachmentSource.ConnectionClipPlaneEntity.GetGlobalFrame();
        var target = attachment.AttachmentTarget.ConnectionClipPlaneEntity.GetGlobalFrame();
        var direction = (target.origin - source.origin).NormalizedCopy();
        // Preserve native current-endpoint eligibility, but never search for a replacement endpoint.
        if (!(source.origin.DistanceSquared(target.origin) < attachment.AttachmentSource.BridgeConnectionLengthSquared
            && attachment.CalculateRelativeVelocityBetweenAttachments().LengthSquared <= 4f
            && Vec2.DotProduct(source.rotation.f.AsVec2.Normalized(), direction.AsVec2) > 0.18f
            && Vec2.DotProduct(target.rotation.f.AsVec2.Normalized(), -direction.AsVec2) > 0.18f)) return false;
        forceBridge = true;
        return true;
    }

    private string RequestPlank(ShipAttachmentMachine source)
    {
        var attachment = source.CurrentAttachment;
        if (attachment == null || attachment.AttachmentTarget == null || attachment.ShipAttachmentJoint == null)
            return "rejected:plank_requires_attached_rope";
        if (attachment.State == RopeState.BridgeThrown || attachment.State == RopeState.BridgeConnected)
            return "already_plank";
        if (attachment.State != RopeState.RopesPulling) return "rejected:plank_requires_pulling";
        attachment.CheckAndConnectBridge(forceBridge: true);
        return attachment.State == RopeState.BridgeThrown
            ? "dispatched:native_forced_plank_bypasses_distance_speed_facing_not_ship_teleport"
            : "rejected:native_plank_not_started";
    }

    private static float[] CapturePlankFlight(Attachment attachment)
    {
        if (attachment.State != RopeState.BridgeThrown && attachment.State != RopeState.BridgeConnected) return null;
        var value = attachment._bridgeFlightData;
        return new[] { value.DtSinceFlightStart, value.CurveLerpVelocity, value.CurveLerpValue,
            value.ThrowFinishValue, value.CurrentFrameTotalLightTime, value.CurrentFrameInitialVelocity.x,
            value.CurrentFrameInitialVelocity.y, value.CurrentFrameInitialVelocity.z };
    }

    private void ApplyPlank(Attachment attachment, NetworkNavalLabRopeState value)
    {
        bool plank = value.State == (int)RopeState.BridgeThrown || value.State == (int)RopeState.BridgeConnected;
        if (!plank) return;
        if (attachment.State != RopeState.BridgeThrown && attachment.State != RopeState.BridgeConnected)
            attachment.StartBridgeThrowAnimation();
        var f = value.PlankFlight;
        attachment._bridgeFlightData = new Attachment.BridgeFlightData
        {
            DtSinceFlightStart = f[0], CurveLerpVelocity = f[1], CurveLerpValue = f[2], ThrowFinishValue = f[3],
            CurrentFrameTotalLightTime = f[4], CurrentFrameInitialVelocity = new Vec3(f[5], f[6], f[7])
        };
        if (value.State == (int)RopeState.BridgeConnected && attachment.State != RopeState.BridgeConnected)
        {
            attachment.ConnectBridge();
            // Apply occurs outside the source machine's tick transition observer.
            attachment.AttachmentSource.OwnerShip.OnShipConnected(attachment);
            attachment.AttachmentTarget.OwnerShip.OnShipConnected(attachment);
        }
    }

    internal bool TickReplicaPlankFlight(Attachment attachment)
    {
        if (!IsFixtureRope(attachment) || IsOwnedRope(attachment)) return true;
        // Owner flight progress is installed with the sample; never run a second completion clock.
        attachment.ArrangePlanksMT();
        attachment.ArrangePlanks();
        return false;
    }

    internal bool AllowAttachmentBreakCheck(Attachment attachment) => !IsFixtureRope(attachment) || IsOwnedRope(attachment);

    private int PlankOccupants(Attachment attachment)
    {
        if (Mission == null || Mission != TaleWorlds.MountAndBlade.Mission.Current) return -1;
        int count = 0;
        foreach (var agent in Mission.Agents)
        {
            if (!agent.IsActive()) continue;
            if (agent.Pointer == UIntPtr.Zero) return -1;
            if (IsOnPlank(agent, attachment)) count++;
        }
        return count;
    }

    // Native plank membership: stepped bridge or ramp entity, or a face of the plank's generated navmesh.
    private static bool IsOnPlank(Agent agent, Attachment attachment)
    {
        int face = agent.GetCurrentNavigationFaceId();
        if (attachment._navMeshBridge != null && face >= attachment._bridgeNavmeshId && face <= attachment._bridgeNavmeshId + 4)
            return true;
        var stepped = agent.GetSteppedEntity();
        if (!stepped.IsValid) return false;
        var bridge = attachment._bridge?.WeakEntity ?? WeakGameEntity.Invalid;
        var navmesh = attachment._navMeshBridge?.WeakEntity ?? WeakGameEntity.Invalid;
        return stepped == attachment.AttachmentSource.PlankBridgePhysicsEntity.WeakEntity
            || (bridge.IsValid && stepped.Root == bridge) || (navmesh.IsValid && stepped.Root == navmesh)
            || attachment.AttachmentSource.RampPhysicsList.Any(entity => entity.WeakEntity == stepped)
            || (attachment.AttachmentTarget != null && attachment.AttachmentTarget.RampPhysicsList.Any(entity => entity.WeakEntity == stepped));
    }

    // A captain on a connected fixture plank uses the rope's source hull as an approximate support frame.
    private int PlankSupportSlot(Agent agent)
    {
        if (ropeSources == null) return -1;
        foreach (var source in ropeSources.SelectMany(row => row))
        {
            var attachment = source.CurrentAttachment;
            if (attachment?.State == RopeState.BridgeConnected && IsOnPlank(agent, attachment))
                return Array.IndexOf(Ships, source.OwnerShip);
        }
        return -1;
    }

    internal bool AllowPlankRemoval(Attachment attachment)
    {
        if (!IsFixtureRope(attachment) || (attachment.State != RopeState.BridgeThrown
            && attachment.State != RopeState.BridgeConnected && attachment._navMeshBridge == null)) return true;
        try
        {
            if (PlankOccupants(attachment) == 0 && attachment.CommittedAgentCount == 0
                && attachment.AttachmentSource.SteppedAgentManager.AgentCount == 0) return true;
        }
        catch (Exception exception) { Reject("plank.occupancy_unavailable:" + exception.GetType().Name); return false; }
        Reject("plank.occupied_or_unknown_removal_blocked_process_exit_required");
        return false;
    }

    internal Random PlankCosmeticRandom(Attachment attachment, string purpose)
    {
        if (!IsFixtureRope(attachment)) return null;
        ropes.TryGetValue(attachment.AttachmentSource, out var record);
        if (purpose == "AddRopesToBridge" && IsOwnedRope(attachment) && record != null)
            record.DecorationPlanks = attachment._numberOfPlanksNeeded;
        long generation = record?.Generation ?? 0;
        if (!applyingRope && !ReferenceEquals(record?.Attachment, attachment)) generation++;
        int slot = Array.IndexOf(Ships, attachment.AttachmentSource.OwnerShip);
        string key = manifest.Ships[slot].ToString("N") + ":" + EntityKey(attachment.AttachmentSource.GameEntity, Ships[slot])
            + ":" + generation.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + purpose;
        int seed = 17;
        unchecked { foreach (char value in key) seed = (seed * 31) + value; }
        return new Random(seed);
    }

    internal int PlankDecorationCount(Attachment attachment)
    {
        if (!IsFixtureRope(attachment)) return attachment._numberOfPlanksNeeded;
        if (!ropes.TryGetValue(attachment.AttachmentSource, out var record)) throw new InvalidOperationException("plank.missing_generation");
        return record.DecorationPlanks;
    }

    private object NearbyOccupiedOars(ShipAttachmentMachine source, ShipAttachmentPointMachine target) => new
    {
        source = source.OwnerShip.LeftSideShipOarMachines.Concat(source.OwnerShip.RightSideShipOarMachines)
            .Count(oar => oar.PilotStandingPoint.UserAgent != null
                && oar.GameEntity.GlobalPosition.DistanceSquared(source.ConnectionClipPlaneEntity.GetGlobalFrame().origin) < 9f),
        target = target == null ? (int?)null : target.OwnerShip.LeftSideShipOarMachines.Concat(target.OwnerShip.RightSideShipOarMachines)
            .Count(oar => oar.PilotStandingPoint.UserAgent != null
                && oar.GameEntity.GlobalPosition.DistanceSquared(target.ConnectionClipPlaneEntity.GetGlobalFrame().origin) < 9f)
    };

    private object InspectPlank(Attachment attachment)
    {
        if (attachment == null) return null;
        bool connected = attachment.State == RopeState.BridgeConnected;
        return new
        {
            state = attachment.State.ToString(), flight = CapturePlankFlight(attachment),
            renderEntityValid = attachment._bridge?.WeakEntity.IsValid == true,
            navmeshEntityValid = attachment._navMeshBridge?.WeakEntity.IsValid == true,
            physicsEntityValid = attachment.AttachmentSource.PlankBridgePhysicsEntity.WeakEntity.IsValid,
            generatedPhysicsVertices = attachment._currentFramePlankPhysicsVertexCount,
            generatedPhysicsIndices = attachment._currentFramePlankPhysicsIndexCount,
            physicsBodyFlags = (uint)attachment.AttachmentSource.PlankBridgePhysicsEntity.PhysicsDescBodyFlag,
            localNavmeshId = attachment._navMeshBridge == null ? (int?)null : attachment._bridgeNavmeshId,
            attachment.IsNavmeshConnected, attachment.ShipIslandsConnected,
            facesDisabled = attachment._isNavmeshBridgeDisabled,
            nativeOccupants = connected || attachment.State == RopeState.BridgeThrown ? PlankOccupants(attachment) : (int?)null,
            attachment.CommittedAgentCount, steppedCount = attachment.AttachmentSource.SteppedAgentManager.AgentCount,
            decorationPlanks = ropes.TryGetValue(attachment.AttachmentSource, out var record) ? record.DecorationPlanks : 0,
            crossingVerified = false, occupancyIsAtomic = false
        };
    }
}
#endif
