using ProtoBuf;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public struct AgentData
    {
        public AgentData(
            Agent agent,
            ushort mountMovementId = 0,
            string mountIdentityScopeId = null,
            System.Guid mountAgentId = default)
        {
            Position = agent.Position;
            MovementDirection = agent.GetMovementDirection();
            LookDirection = agent.LookDirection;
            InputVector = agent.MovementInputVector;
            Speed = agent.GetRealGlobalVelocity().AsVec2.Length;

            // The rider can be active while its mount is mid-teardown (e.g. right after a battle concludes):
            // reading the mount's native state (MovementInputVector, etc.) then access-violates. Only capture
            // the mount while it is itself active — mirrors the rider guard in AgentMovementHandler.PollMovement
            // and the horse.IsActive() check in SyncMountState.
            Agent mount = agent.MountAgent;
            if (mount != null && mount.IsActive())
            {
                MountData = new AgentMountData(
                    mount, mountMovementId, mountIdentityScopeId, mountAgentId);
            }
            else
            {
                MountData = null;
            }
        }

        public AgentData(Agent agent, System.Guid mountAgentId)
            : this(agent, 0, null, mountAgentId)
        {
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

            // The raw owner input is local-frame and unrepresentative for AI movement modes (native retreat
            // drives the owner with no input), so an on-foot puppet fed it walks while its position target
            // sprints — the walk-lag-snap desync. Human locomotion is procedural from the input, so derive
            // the throttle from the owner's real ground speed; keep the owner's strafe direction when it has
            // one. Mounted riders keep the raw input — the mount's pace rides its synced channel-0 gait.
            if (agent.HasMount)
            {
                agent.MovementInputVector = InputVector;
            }
            else
            {
                float maxSpeed = agent.GetMaximumForwardUnlimitedSpeed();
                float throttle = maxSpeed > 0f ? MBMath.ClampFloat(Speed / maxSpeed, 0f, 1f) : 0f;
                agent.MovementInputVector = InputVector.LengthSquared > 0.0001f
                    ? InputVector.Normalized() * throttle
                    : new Vec2(0f, throttle);
            }

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
        // 5 was AgentEquipmentData — wield state moved to reliable on-change updates.
        // 6 was ActionData — actions moved to the event-driven AgentActionHandler.
        [ProtoMember(7)]
        public AgentMountData MountData { get; }
        /// <summary>The owner's real ground speed, m/s — drives the on-foot puppet's locomotion throttle.</summary>
        [ProtoMember(8)]
        public float Speed { get; }
    }
}
