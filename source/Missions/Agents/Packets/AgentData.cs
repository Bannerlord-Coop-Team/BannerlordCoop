using ProtoBuf;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public struct AgentData
    {
        // mountId: the mount's registry id (resolved by the caller — this ctor has no registry access), carried
        // so the receiver can attach the puppet to the exact horse; Guid.Empty when unregistered/unmounted.
        public AgentData(Agent agent, System.Guid mountId = default)
        {
            Position = agent.Position;
            MovementDirection = agent.GetMovementDirection();
            LookDirection = agent.LookDirection;
            InputVector = agent.MovementInputVector;

            AgentEquipment = new AgentEquipmentData(agent);

            // The rider can be active while its mount is mid-teardown (e.g. right after a battle concludes):
            // reading the mount's native state (MovementInputVector, etc.) then access-violates. Only capture
            // the mount while it is itself active — mirrors the rider guard in AgentMovementHandler.PollMovement
            // and the horse.IsActive() check in SyncMountState.
            Agent mount = agent.MountAgent;
            if (mount != null && mount.IsActive())
            {
                MountData = new AgentMountData(mount, mountId);
            }
            else
            {
                MountData = null;
            }
        }

        public void Apply(Agent agent)
        {
            // if the player is dead, dont sync anything
            if (agent.Health <= 0)
            {
                return;
            }

            // NOTE: position is NOT applied here. It is reconciled per-frame by AgentPositionInterpolator (fed
            // this packet's Position by AgentMovementHandler), so the ease is decoupled from the packet cadence.
            // Everything below is per-packet state that drives the puppet's own walk + animation.

            agent.SetMovementDirection(MovementDirection);

            // apply the agent's look direction
            agent.LookDirection = LookDirection;
            agent.MovementInputVector = InputVector;
            PuppetMovementComponent.Apply(agent, InputVector, MovementDirection, LookDirection, Position);

            // Update equipment
            AgentEquipment.Apply(agent);

            // NOTE: actions/animations are NOT applied here anymore. They are events, not continuous state, so
            // they are synced separately and on-change by AgentActionHandler (reliable-ordered), not polled with
            // movement. This keeps the movement packet purely continuous state.

            // Update mount
            if (agent.HasMount)
            {
                MountData?.ApplyMount(agent.MountAgent);
            }
        }

        [ProtoMember(1)]
        public Vec3 Position { get; }
        [ProtoMember(2)]
        public Vec2 InputVector { get; }
        [ProtoMember(3)]
        public Vec3 LookDirection { get; }
        [ProtoMember(4)]
        public Vec2 MovementDirection { get; }
        [ProtoMember(5)]
        public AgentEquipmentData AgentEquipment { get; }
        // 6 was ActionData — actions moved to the event-driven AgentActionHandler; tag left unused for wire stability.
        [ProtoMember(7)]
        public AgentMountData MountData { get; }
    }
}
