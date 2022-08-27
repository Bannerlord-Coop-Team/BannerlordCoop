using ProtoBuf;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Packets.Agents
{
    [ProtoContract(SkipConstructor = true)]
    public class AgentMountData
    {
        public AgentMountData(Agent agent)
        {
            MountInputVector = agent.MountAgent.MovementInputVector;
            MountAction1Flag = (ulong)agent.MountAgent.GetCurrentAnimationFlag(1);
            MountAction1Progress = agent.MountAgent.GetCurrentActionProgress(1);
            MountAction1Index = agent.MountAgent.GetCurrentAction(1).Index;
            MountLookDirection = agent.MountAgent.LookDirection;
            MountMovementDirection = agent.MountAgent.GetMovementDirection();
            MountPosition = agent.MountAgent.Position;
        }

        public void ApplyMount(Agent mountAgent)
        {
            Vec3 mountPos = MountPosition;

            if (mountAgent.GetPathDistanceToPoint(ref mountPos) > 5f)
            {
                mountAgent.TeleportToPosition(mountPos);
            }
            mountAgent.SetMovementDirection(MountMovementDirection);

            //Currently not doing anything afaik
            if (mountAgent.GetCurrentAction(1) == ActionIndexCache.act_none || mountAgent.GetCurrentAction(1).Index != MountAction1Index)
            {
                string mActionName2 = MBAnimation.GetActionNameWithCode(MountAction1Index);
                mountAgent.SetActionChannel(1, ActionIndexCache.Create(mActionName2), additionalFlags: MountAction1Flag, startProgress: MountAction1Progress);
            }
            else
            {
                mountAgent.SetCurrentActionProgress(1, MountAction1Progress);
            }
            mountAgent.LookDirection = MountLookDirection;
            mountAgent.MovementInputVector = MountInputVector;
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
    }
}
