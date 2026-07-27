using ProtoBuf;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public class AgentMountData
    {
        // The parameter is the MOUNT agent itself (callers pass rider.MountAgent), so read it directly —
        // mirroring ApplyMount. Dereferencing .MountAgent here was reading the mount's own (null) mount → NRE.
        public AgentMountData(
            Agent mountAgent,
            ushort mountMovementId = 0,
            string mountIdentityScopeId = null,
            Guid mountAgentId = default)
        {
            MountInputVector = mountAgent.MovementInputVector;
            MountAction0Flag = (ulong)mountAgent.GetCurrentAnimationFlag(0);
            MountAction0Progress = mountAgent.GetCurrentActionProgress(0);
            MountAction0Index = mountAgent.GetCurrentAction(0).Index;
            MountAction1Flag = (ulong)mountAgent.GetCurrentAnimationFlag(1);
            MountAction1Progress = mountAgent.GetCurrentActionProgress(1);
            MountAction1Index = mountAgent.GetCurrentAction(1).Index;
            MountLookDirection = mountAgent.LookDirection;
            MountMovementDirection = mountAgent.GetMovementDirection();
            MountPosition = mountAgent.Position;
            MountSpeed = mountAgent.GetRealGlobalVelocity().AsVec2.Length;
            MountMovementId = mountMovementId;
            MountIdentityScopeId = mountIdentityScopeId;
            MountAgentId = mountAgentId;
        }

        public AgentMountData(Agent mountAgent, Guid mountAgentId)
            : this(mountAgent, 0, null, mountAgentId)
        {
        }

        public void ApplyMount(Agent mountAgent)
        {
            // NOTE: mount position is NOT applied here — it is reconciled per-frame by AgentPositionInterpolator
            // (fed MountPosition by AgentMovementHandler). Everything below is per-packet mount state/animation.
            mountAgent.SetMovementDirection(MountMovementDirection);

            // Channel 0 is the horse's GAIT (walk/trot/canter/gallop). A Controller.None puppet has no controller
            // to select it, so without replicating the owner's gait action the horse slides with no leg animation.
            if (mountAgent.GetCurrentAction(0) == ActionIndexCache.act_none || mountAgent.GetCurrentAction(0).Index != MountAction0Index)
            {
                string gaitActionName = AgentActionData.GetActionNameWithCode(MountAction0Index);
                if (gaitActionName != null)
                    mountAgent.SetActionChannel(0, ActionIndexCache.Create(gaitActionName), additionalFlags: (AnimFlags)MountAction0Flag, startProgress: MountAction0Progress);
            }
            else
            {
                mountAgent.SetCurrentActionProgress(0, MountAction0Progress);
            }

            //Currently not doing anything afaik
            if (mountAgent.GetCurrentAction(1) == ActionIndexCache.act_none || mountAgent.GetCurrentAction(1).Index != MountAction1Index)
            {
                string mActionName2 = AgentActionData.GetActionNameWithCode(MountAction1Index);
                if (mActionName2 != null)
                    mountAgent.SetActionChannel(1, ActionIndexCache.Create(mActionName2), additionalFlags: (AnimFlags)MountAction1Flag, startProgress: MountAction1Progress);
            }
            else
            {
                mountAgent.SetCurrentActionProgress(1, MountAction1Progress);
            }
            mountAgent.LookDirection = MountLookDirection;
            mountAgent.MovementInputVector = MountInputVector;

            // Controller.None still lets native horse motion persist between replicated position corrections.
            // Cap that motion to the owner's real speed so a stopped owner also stops its puppet horse.
            mountAgent.SetMaximumSpeedLimit(MountSpeed, isMultiplier: false);
        }

        [ProtoMember(1)]
        public Vec2 MountInputVector { get; }
        [ProtoMember(2)]
        public ulong MountAction1Flag { get; }
        [ProtoMember(3)]
        public float MountAction1Progress { get; }
        [ProtoMember(4)]
        public int MountAction1Index { get; }
        [ProtoMember(5)]
        public Vec3 MountLookDirection { get; }
        [ProtoMember(6)]
        public Vec2 MountMovementDirection { get; }
        [ProtoMember(7)]
        public Vec3 MountPosition { get; }
        /// <summary>The mount's owner-scoped movement id, or zero when the horse is unregistered.</summary>
        [ProtoMember(8)]
        public ushort MountMovementId { get; }
        [ProtoMember(9)]
        public ulong MountAction0Flag { get; }
        [ProtoMember(10)]
        public float MountAction0Progress { get; }
        [ProtoMember(11)]
        public int MountAction0Index { get; }
        /// <summary>The owner's horizontal mount speed, used as the puppet's absolute native speed limit.</summary>
        [ProtoMember(12)]
        public float MountSpeed { get; }
        /// <summary>Only populated when the mount's original owner differs from the rider's identity scope.</summary>
        [ProtoMember(13)]
        public string MountIdentityScopeId { get; }
        [ProtoMember(14)]
        public Guid MountAgentId { get; }
    }
}
