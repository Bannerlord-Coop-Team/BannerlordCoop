using ProtoBuf;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public struct AgentData
    {
        internal static Agent.MovementControlFlag GetLocomotionMovementFlags(
            Agent.MovementControlFlag movementFlags)
        {
            return movementFlags & Agent.MovementControlFlag.MoveMask;
        }

        internal static void ApplyLocomotionMovementFlags(
            Agent agent,
            Agent.MovementControlFlag movementFlags)
        {
            Agent.MovementControlFlag currentMovementFlags = agent.MovementFlags;
            Agent.MovementControlFlag currentFlags =
                currentMovementFlags & ~Agent.MovementControlFlag.MoveMask;
            Agent.MovementControlFlag desiredMovementFlags =
                currentFlags |
                GetLocomotionMovementFlags(movementFlags);
            if (currentMovementFlags == desiredMovementFlags)
                return;

            agent.MovementFlags = desiredMovementFlags;
        }

        internal static void ApplyMovementDirection(
            Agent agent,
            Vec2 movementDirection)
        {
            Vec2 current = agent.GetMovementDirection();
            if (current.X == movementDirection.X &&
                current.Y == movementDirection.Y)
            {
                return;
            }

            agent.SetMovementDirection(movementDirection);
        }

        internal static void ApplyLookDirection(
            Agent agent,
            Vec3 lookDirection)
        {
            Vec3 current = agent.LookDirection;
            if (current.X == lookDirection.X &&
                current.Y == lookDirection.Y &&
                current.Z == lookDirection.Z)
            {
                return;
            }

            agent.LookDirection = lookDirection;
        }

        internal static void ApplyMovementInput(
            Agent agent,
            Vec2 movementInput)
        {
            Vec2 current = agent.MovementInputVector;
            if (current.X == movementInput.X &&
                current.Y == movementInput.Y)
            {
                return;
            }

            agent.MovementInputVector = movementInput;
        }

        public AgentData(
            Agent agent,
            ushort mountMovementId = 0,
            string mountIdentityScopeId = null,
            System.Guid mountAgentId = default,
            int? mountAction0TurnDirection = null,
            int? mountAction0TurnActionIndex = null,
            float? mountAction0TurnProgress = null,
            bool? mountAction0IsSyntheticTurn = null)
        {
            Position = agent.Position;
            MovementDirection = agent.GetMovementDirection();
            LookDirection = agent.LookDirection;
            InputVector = agent.MovementInputVector;
            Speed = agent.GetRealGlobalVelocity().AsVec2.Length;
            MovementFlag = (uint)GetLocomotionMovementFlags(
                agent.MovementFlags);

            // The rider can be active while its mount is mid-teardown (e.g. right after a battle concludes):
            // reading the mount's native state (MovementInputVector, etc.) then access-violates. Only capture
            // the mount while it is itself active — mirrors the rider guard in AgentMovementHandler.PollMovement
            // and the horse.IsActive() check in SyncMountState.
            Agent mount = agent.MountAgent;
            if (mount != null && mount.IsActive())
            {
                MountData = new AgentMountData(
                    mount,
                    mountMovementId,
                    mountIdentityScopeId,
                    mountAgentId,
                    mountAction0TurnDirection: mountAction0TurnDirection,
                    mountAction0TurnActionIndex: mountAction0TurnActionIndex,
                    mountAction0TurnProgress: mountAction0TurnProgress,
                    mountAction0IsSyntheticTurn: mountAction0IsSyntheticTurn);
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

            ApplyContinuousState(agent);

            // NOTE: actions/animations are NOT applied here anymore. They are events, not continuous state, so
            // they are synced separately and on-change by AgentActionHandler (reliable-ordered), not polled with
            // movement. This keeps the movement packet purely continuous state.

            // Update mount
            if (agent.HasMount)
            {
                MountData?.ApplyMount(agent.MountAgent);
            }
        }

        internal void ApplyContinuousState(Agent agent)
        {
            ApplyMovementDirection(agent, MovementDirection);
            ApplyLookDirection(agent, LookDirection);
            ApplyMovementInput(agent, GetMovementInput(agent));
            ApplyLocomotionMovementFlags(
                agent,
                (Agent.MovementControlFlag)MovementFlag);
        }

        internal Vec2 GetMovementInput(Agent agent)
        {
            // The raw owner input is local-frame and unrepresentative for AI movement modes (native retreat
            // drives the owner with no input), so derive an on-foot puppet's throttle from ground speed.
            if (agent.HasMount)
                return InputVector;

            float maxSpeed = agent.GetMaximumForwardUnlimitedSpeed();
            float throttle = maxSpeed > 0f
                ? MBMath.ClampFloat(Speed / maxSpeed, 0f, 1f)
                : 0f;
            return InputVector.LengthSquared > 0.0001f
                ? InputVector.Normalized() * throttle
                : new Vec2(0f, throttle);
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
        /// <summary>The owner's current translation and turn inputs.</summary>
        [ProtoMember(9)]
        public uint MovementFlag { get; }
    }
}
